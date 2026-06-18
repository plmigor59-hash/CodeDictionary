using CodeDictionary.Analysis;
using OneScript.Language.LexicalAnalysis;
using OneScript.Language.SyntaxAnalysis;
using OneScript.Language.SyntaxAnalysis.AstNodes;

namespace CodeDictionary.SyntaxChecking
{
    public enum ErrorType
    {
        UndeclaredVariable,
        Redeclaration,
        UndeclaredMethod,
        TypeMismatch
    }

    public class SymbolExtractor : BslSyntaxWalker
    {
        public List<SymbolInfo> Symbols { get; } = new();
        public List<CodeSyntaxError> Errors { get; } = new();

        private readonly HashSet<string> _declaredMethods = new(StringComparer.OrdinalIgnoreCase);
        private readonly Stack<HashSet<string>> _variableScopes = new();
        private readonly Dictionary<string, string> _variableTypes = new(StringComparer.OrdinalIgnoreCase);
        private string _currentMethodName = string.Empty;

        public SymbolExtractor()
        {
            _variableScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        public IReadOnlyDictionary<string, string> VariableTypes => _variableTypes;

        protected override void VisitModule(ModuleNode node)
        {
            base.VisitModule(node);
        }

        protected override void VisitMethod(MethodNode node)
        {
            var previousMethod = _currentMethodName;
            _currentMethodName = node.Signature?.MethodName ?? "Unknown";

            _variableScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            var methodName = node.Signature?.MethodName ?? "Unknown";
            var isFunction = node.Signature?.IsFunction ?? false;

            if (_declaredMethods.Contains(methodName))
            {
                AddError($"Метод '{methodName}' уже объявлен",
                    node.Signature?.Location ?? CodeRange.EmptyRange(),
                    ErrorType.Redeclaration);
            }
            else
            {
                _declaredMethods.Add(methodName);

                var paramNames = node.Signature?.GetParameters()
                    .Select(p => p.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToArray() ?? [];

                var paramTypes = node.Signature?.GetParameters()
                    .Select(p => (string?)null)
                    .ToArray() ?? [];

                Symbols.Add(new SymbolInfo
                {
                    Name = methodName,
                    Type = isFunction ? "Функция" : "Процедура",
                    Line = node.Signature?.Location.LineNumber ?? 0,
                    Column = node.Signature?.Location.ColumnNumber ?? 0,
                    ParameterNames = paramNames!,
                    ParameterTypes = paramTypes!
                });
            }

            base.VisitMethod(node);

            _variableScopes.Pop();
            _currentMethodName = previousMethod;
        }

        protected override void VisitMethodVariable(MethodNode method, VariableDefinitionNode variableDefinition)
        {
            var varName = variableDefinition.Name;
            var currentScope = _variableScopes.Peek();

            if (currentScope.Contains(varName))
            {
                AddError($"Переменная '{varName}' уже объявлена в методе '{_currentMethodName}'",
                    variableDefinition.Location,
                    ErrorType.Redeclaration);
            }
            else
            {
                currentScope.Add(varName);
                AddSymbol(varName, "Локальная переменная", variableDefinition.Location);
            }

            base.VisitMethodVariable(method, variableDefinition);
        }

        protected override void VisitMethodSignature(MethodSignatureNode node)
        {
            if (node == null) return;

            var currentScope = _variableScopes.Peek();

            foreach (var parameter in node.GetParameters())
            {
                if (!string.IsNullOrWhiteSpace(parameter.Name))
                {
                    if (currentScope.Contains(parameter.Name))
                    {
                        AddError($"Параметр '{parameter.Name}' уже объявлен",
                            parameter.Location,
                            ErrorType.Redeclaration);
                    }
                    else
                    {
                        currentScope.Add(parameter.Name);
                        AddSymbol(parameter.Name, "Параметр", parameter.Location);
                    }
                }
            }

            base.VisitMethodSignature(node);
        }

        protected override void VisitVariableRead(TerminalNode node)
        {
            var varName = node.Lexem.Content;

            if (!IsVariableDeclared(varName))
            {
                AddError($"Переменная '{varName}' не объявлена",
                    node.Location,
                    ErrorType.UndeclaredVariable);
            }

            base.VisitVariableRead(node);
        }

        protected override void VisitVariableWrite(TerminalNode node)
        {
            var varName = node.Lexem.Content;

            if (!IsVariableDeclared(varName))
            {
                var currentScope = _variableScopes.Peek();
                currentScope.Add(varName);
                AddSymbol(varName, "Автоматическая переменная", node.Location);
            }

            base.VisitVariableWrite(node);
        }

        protected override void VisitAssignmentLeftPart(BslSyntaxNode node)
        {
            if (node is TerminalNode term && term.Kind == NodeKind.Identifier)
            {
                var varName = term.Lexem.Content;

                if (!IsVariableDeclared(varName))
                {
                    var currentScope = _variableScopes.Peek();
                    currentScope.Add(varName);
                    AddSymbol(varName, "Автоматическая переменная", term.Location);
                }
            }
            base.VisitAssignmentLeftPart(node);
        }

        protected override void VisitIteratorLoopVariable(TerminalNode node)
        {
            var currentScope = _variableScopes.Peek();
            var varName = node.Lexem.Content;

            if (!currentScope.Contains(varName))
            {
                currentScope.Add(varName);
                AddSymbol(varName, "Итератор цикла", node.Location);
            }

            base.VisitIteratorLoopVariable(node);
        }

        // === Вызовы методов (глобальные) ===

        protected override void VisitGlobalFunctionCall(CallNode node)
        {
            CheckAndTrackMethodCall(node);
            // Traverse only argument list, not the method name identifier
            TraverseArgumentList(node);
        }

        protected override void VisitGlobalProcedureCall(CallNode node)
        {
            CheckAndTrackMethodCall(node);
            TraverseArgumentList(node);
        }

        private void CheckAndTrackMethodCall(CallNode node)
        {
            var methodName = node.Identifier?.Lexem.Content;
            if (!string.IsNullOrEmpty(methodName) && !_declaredMethods.Contains(methodName))
            {
                AddError($"Метод '{methodName}' не объявлен",
                    node.Location,
                    ErrorType.UndeclaredMethod);
            }
        }

        private void TraverseArgumentList(CallNode node)
        {
            if (node.ArgumentList != null)
            {
                foreach (var arg in node.ArgumentList.Children)
                {
                    DefaultVisit(arg);
                }
            }
        }

        // === Новые объекты ===

        protected override void VisitNewObjectCreation(NewObjectNode node)
        {
            if (node.TypeNameNode is TerminalNode term)
            {
                var typeName = term.Lexem.Content;

                if (node.Parent?.Parent is NonTerminalNode assign && assign.Kind == NodeKind.Assignment)
                {
                    if (assign.Children[0] is TerminalNode leftTerm && leftTerm.Kind == NodeKind.Identifier)
                    {
                        _variableTypes[leftTerm.Lexem.Content] = typeName;
                    }
                }
            }

            // Traverse constructor arguments
            foreach (var child in node.Children)
            {
                if (child != node.TypeNameNode)
                {
                    DefaultVisit(child);
                }
            }
        }

        // === Операции (пустые в базе — добавляем обход) ===

        protected override void VisitBinaryOperation(BinaryOperationNode node)
        {
            foreach (var child in node.Children)
                DefaultVisit(child);
        }

        protected override void VisitUnaryOperation(UnaryOperationNode node)
        {
            foreach (var child in node.Children)
                DefaultVisit(child);
        }

        protected override void VisitTernaryOperation(BslSyntaxNode node)
        {
            foreach (var child in node.Children)
                DefaultVisit(child);
        }

        // === Вспомогательные методы ===

        private bool IsVariableDeclared(string name)
        {
            foreach (var scope in _variableScopes)
            {
                if (scope.Contains(name))
                    return true;
            }
            return false;
        }

        private void AddError(string message, CodeRange location, ErrorType errorType)
        {
            Errors.Add(new CodeSyntaxError
            {
                Message = message,
                Line = location.LineNumber,
                Column = location.ColumnNumber,
                Length = 1,
                ErrorType = errorType.ToString()
            });
        }

        private void AddSymbol(string name, string type, CodeRange location)
        {
            if (string.IsNullOrWhiteSpace(name)) return;

            Symbols.Add(new SymbolInfo
            {
                Name = name,
                Type = type,
                Line = location.LineNumber,
                Column = location.ColumnNumber
            });
        }

        public void Reset()
        {
            Symbols.Clear();
            Errors.Clear();
            _declaredMethods.Clear();
            _variableTypes.Clear();
            _variableScopes.Clear();
            _variableScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }
    }
}
