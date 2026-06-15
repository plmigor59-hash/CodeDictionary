using CodeDictionary.Analysis;
using OneScript.Language.LexicalAnalysis;
using OneScript.Language.SyntaxAnalysis;
using OneScript.Language.SyntaxAnalysis.AstNodes;
using System.Diagnostics;

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
        public List<BslSyntaxError> Errors { get; } = new();

        private readonly HashSet<string> _declaredMethods = new(StringComparer.OrdinalIgnoreCase);

        // Стек для отслеживания области видимости переменных
        private readonly Stack<HashSet<string>> _variableScopes = new();
        private string _currentMethodName = string.Empty;

        public SymbolExtractor()
        {
            Debug.WriteLine("SymbolExtractor initialized");
            _variableScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase)); // Глобальная область
        }

        // 1. Обход модуля
        protected override void VisitModule(ModuleNode node)
        {
            Debug.WriteLine("=== Visiting Module ===");
            base.VisitModule(node);
        }

        // 2. Методы и процедуры
        protected override void VisitMethod(MethodNode node)
        {
            // Сохраняем текущий метод
            var previousMethod = _currentMethodName;
            _currentMethodName = node.Signature?.MethodName ?? "Unknown";

            // Создаем новую область видимости для переменных метода
            _variableScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            try
            {
                var methodName = node.Signature?.MethodName ?? "Unknown";
                var isFunction = node.Signature?.IsFunction ?? false;

                Debug.WriteLine($"Found method: {methodName}, IsFunction: {isFunction}");

                //Проверка на повторное объявление метода
                if (_declaredMethods.Contains(methodName))
                {
                    AddError($"Метод '{methodName}' уже объявлен",
                        node.Signature?.Location ?? CodeRange.EmptyRange(),
                        ErrorType.Redeclaration);
                }
                else
                {
                    _declaredMethods.Add(methodName);
                    AddSymbol(methodName, isFunction ? "Функция" : "Процедура",
                        node.Signature?.Location ?? CodeRange.EmptyRange());
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in VisitMethod: {ex.Message}");
            }

            base.VisitMethod(node);

            // Выходим из области метода
            _variableScopes.Pop();
            _currentMethodName = previousMethod;
        }

        protected override void VisitMethodVariable(MethodNode method, VariableDefinitionNode variableDefinition)
        {
            var varName = variableDefinition.Name;
            var currentScope = _variableScopes.Peek();

            // Проверка на повторное объявление в текущей области
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
                    // Проверка на повторное объявление параметра
                    if (currentScope.Contains(parameter.Name))
                    {
                        AddError($"Параметр '{parameter.Name}' уже объявлен",
                            GetLocation(parameter),
                            ErrorType.Redeclaration);
                    }
                    else
                    {
                        currentScope.Add(parameter.Name);
                        AddSymbol(parameter.Name, "Параметр", GetLocation(parameter));
                    }
                }
            }

            base.VisitMethodSignature(node);
        }

        // Проверка использования переменных (чтение)
        protected override void VisitVariableRead(TerminalNode node)
        {
            var varName = node.Lexem.Content;

            // Проверяем, объявлена ли переменная
            if (!IsVariableDeclared(varName))
            {
                AddError($"Переменная '{varName}' не объявлена",
                    node.Location,
                    ErrorType.UndeclaredVariable);
            }

            base.VisitVariableRead(node);
        }

        // Проверка записи в переменную
        protected override void VisitVariableWrite(TerminalNode node)
        {
            var varName = node.Lexem.Content;

            // Проверяем, объявлена ли переменная
            if (!IsVariableDeclared(varName))
            {
                var currentScope = _variableScopes.Peek();
                currentScope.Add(varName);
                AddSymbol(varName, "Автоматическая переменная", GetLocation(node));
                Debug.WriteLine($"  >>> Auto-declared variable on write: {varName}");
            }

            base.VisitVariableWrite(node);
        }

        // Проверка левой части присваивания
        protected override void VisitAssignmentLeftPart(BslSyntaxNode node)
        {
            if (node is TerminalNode term && term.Kind == NodeKind.Identifier)
            {
                var varName = term.Lexem.Content;

                if (!IsVariableDeclared(varName))
                {
                    var currentScope = _variableScopes.Peek();
                    currentScope.Add(varName);
                    AddSymbol(varName, "Автоматическая переменная", GetLocation(term));
                    Debug.WriteLine($"  >>> Auto-declared variable: {varName}");
                }
            }
            base.VisitAssignmentLeftPart(node);
        }

        // Итератор цикла (для циклов For Each)
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

        // Вспомогательный метод проверки объявления переменной
        private bool IsVariableDeclared(string name)
        {
            // Проверяем во всех областях видимости (от текущей к глобальной)
            foreach (var scope in _variableScopes)
            {
                if (scope.Contains(name))
                    return true;
            }

            return false;
        }

        // Добавление ошибки
        private void AddError(string message, CodeRange location, ErrorType errorType)
        {
            Errors.Add(new BslSyntaxError
            {
                Message = message,
                Line = location.LineNumber,
                Column = location.ColumnNumber,
                Length = 1, // Will be updated by CodeAnalyzer
                ErrorType = errorType.ToString()
            });

            Debug.WriteLine($"  ❌ Error at line {location.LineNumber}: {message}");
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

            Debug.WriteLine($"  >>> Added symbol: {name} ({type}) at line {location.LineNumber}, col {location.ColumnNumber}");
        }

        public void Reset()
        {
            Symbols.Clear();
            Errors.Clear();
            _declaredMethods.Clear();
            _variableScopes.Clear();
            _variableScopes.Push(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        // Вспомогательные методы для работы с рефлексией

        private object GetPropertyValue(object obj, string propertyName)
        {
            if (obj == null) return null;
            var prop = obj.GetType().GetProperty(propertyName);
            return prop?.GetValue(obj);
        }

        private string GetVariableName(object variableNode)
        {
            if (variableNode == null) return null;

            // Пробуем получить имя через свойство Name
            var name = GetPropertyValue(variableNode, "Name")?.ToString();
            if (!string.IsNullOrEmpty(name)) return name;

            // Пробуем через Lexem
            var lexem = GetPropertyValue(variableNode, "Lexem");
            if (lexem != null)
            {
                var content = GetPropertyValue(lexem, "Content")?.ToString();
                if (!string.IsNullOrEmpty(content)) return content;
            }

            return null;
        }

        private string GetMethodNameFromCall(object callNode)
        {
            if (callNode == null) return null;

            // Пробуем получить Callee (вызываемый объект)
            var callee = GetPropertyValue(callNode, "Callee");
            if (callee != null)
            {
                var name = GetVariableName(callee);
                if (!string.IsNullOrEmpty(name)) return name;
            }

            // Пробуем MethodName
            var methodName = GetPropertyValue(callNode, "MethodName")?.ToString();
            if (!string.IsNullOrEmpty(methodName)) return methodName;

            // Пробуем Name
            var name2 = GetPropertyValue(callNode, "Name")?.ToString();
            if (!string.IsNullOrEmpty(name2)) return name2;

            return null;
        }

        // Рекурсивный обход для анализа вызовов методов и других конструкций
        private void AnalyzeNode(object node)
        {
            if (node == null) return;

            var nodeType = node.GetType().Name;

            switch (nodeType)
            {
                case "Preprocessor":
                    Debug.WriteLine($"  Skipping preprocessor directive: {nodeType}");
                    break;
                case "CallNode":
                    AnalyzeMethodCall(node);
                    break;
                case "AssignmentNode":
                    AnalyzeAssignment(node);
                    break;
                case "IfStatementNode":
                    AnalyzeIfStatement(node);
                    break;
                case "WhileLoopNode":
                    AnalyzeWhileLoop(node);
                    break;
                case "ForLoopNode":
                    AnalyzeForLoop(node);
                    break;
                case "ReturnNode":
                    AnalyzeReturn(node);
                    break;
                default:
                    var children = GetChildren(node);
                    foreach (var child in children)
                    {
                        AnalyzeNode(child);
                    }
                    break;
            }
        }

        private void AnalyzeMethodCall(object callNode)
        {
            var methodName = GetMethodNameFromCall(callNode);

            if (!string.IsNullOrEmpty(methodName))
            {
                Debug.WriteLine($"    Method call: {methodName}");

                // Проверяем существование метода
                if (!_declaredMethods.Contains(methodName))
                {
                    var location = GetLocation(callNode);
                    AddError($"Метод '{methodName}' не объявлен",
                        location,
                        ErrorType.UndeclaredMethod);
                }
            }

            // Анализируем аргументы
            var args = GetPropertyValue(callNode, "Arguments");
            if (args is IEnumerable<object> arguments)
            {
                foreach (var arg in arguments)
                {
                    AnalyzeNode(arg);
                }
            }
        }

        private void AnalyzeAssignment(object assignmentNode)
        {
            var rightPart = GetPropertyValue(assignmentNode, "Right");
            if (rightPart != null)
            {
                AnalyzeNode(rightPart);
            }
        }

        private void AnalyzeIfStatement(object ifNode)
        {
            var condition = GetPropertyValue(ifNode, "Condition");
            if (condition != null) AnalyzeNode(condition);

            var thenBlock = GetPropertyValue(ifNode, "ThenBlock");
            if (thenBlock != null) AnalyzeNode(thenBlock);

            var elseBlock = GetPropertyValue(ifNode, "ElseBlock");
            if (elseBlock != null) AnalyzeNode(elseBlock);
        }

        private void AnalyzeWhileLoop(object whileNode)
        {
            var condition = GetPropertyValue(whileNode, "Condition");
            if (condition != null) AnalyzeNode(condition);

            var body = GetPropertyValue(whileNode, "Body");
            if (body != null) AnalyzeNode(body);
        }

        private void AnalyzeForLoop(object forNode)
        {
            var initializer = GetPropertyValue(forNode, "Initializer");
            if (initializer != null) AnalyzeNode(initializer);

            var condition = GetPropertyValue(forNode, "Condition");
            if (condition != null) AnalyzeNode(condition);

            var iterator = GetPropertyValue(forNode, "Iterator");
            if (iterator != null) AnalyzeNode(iterator);

            var body = GetPropertyValue(forNode, "Body");
            if (body != null) AnalyzeNode(body);
        }

        private void AnalyzeReturn(object returnNode)
        {
            var returnValue = GetPropertyValue(returnNode, "Value");
            if (returnValue != null) AnalyzeNode(returnValue);
        }

        private IEnumerable<object> GetChildren(object node)
        {
            if (node == null) return Enumerable.Empty<object>();

            var prop = node.GetType().GetProperty("Children");
            if (prop != null && prop.GetValue(node) is IEnumerable<object> children)
                return children;

            return Enumerable.Empty<object>();
        }

        private CodeRange GetLocation(object node)
        {
            if (node == null) return new CodeRange();

            // 1. Try to get Location property directly
            var locationProp = node.GetType().GetProperty("Location");
            if (locationProp != null)
            {
                var location = locationProp.GetValue(node);
                if (location is CodeRange codeRange && codeRange.LineNumber > 0)
                    return codeRange;
            }

            // 2. Try to get location from Name property if it exists
            var nameProp = node.GetType().GetProperty("Name");
            if (nameProp != null)
            {
                var nameValue = nameProp.GetValue(node);
                if (nameValue != null)
                {
                    var nameLocation = GetLocation(nameValue);
                    if (nameLocation.LineNumber > 0)
                        return nameLocation;
                }
            }

            // 3. Try to find the first child with a valid location
            var children = GetChildren(node);
            foreach (var child in children)
            {
                var childLocation = GetLocation(child);
                if (childLocation.LineNumber > 0)
                    return childLocation;
            }

            return new CodeRange();
        }

        private CodeRange GetLocation(MethodSignatureNode node)
        {
            if (node == null) return new CodeRange();
            return GetLocation((object)node);
        }

        protected override void VisitForLoopInitialValue(BslSyntaxNode node)
        {
            if (node != null) AnalyzeNode(node);
            base.VisitForLoopInitialValue(node);
        }

        protected override void VisitForLoopIterator(BslSyntaxNode node)
        {
            if (node != null) AnalyzeNode(node);
            base.VisitForLoopIterator(node);
        }

        protected override void VisitPreprocessorDirective(PreprocessorDirectiveNode node)
        {
            Debug.WriteLine($"  Ignoring preprocessor directive: {node.GetType().Name}");
        }
    }
}
