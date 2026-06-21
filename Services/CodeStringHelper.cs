namespace CodeDictionary.Services
{
    internal static class CodeStringHelper
    {
        public static bool IsInsideStringOrComment(string code, int offset)
        {
            if (string.IsNullOrEmpty(code) || offset <= 0 || offset > code.Length)
                return false;

            int adjustedOffset = Math.Min(offset - 1, code.Length - 1);

            bool inSingleComment = false;
            bool inMultiComment = false;
            bool inString = false;
            char stringChar = '"';

            for (int i = 0; i <= adjustedOffset; i++)
            {
                char c = code[i];

                if (inSingleComment)
                {
                    if (c == '\n') inSingleComment = false;
                    continue;
                }

                if (inMultiComment)
                {
                    if (c == '*' && i + 1 < code.Length && code[i + 1] == '/')
                    {
                        inMultiComment = false;
                        i++;
                    }
                    continue;
                }

                if (inString)
                {
                    if (c == '\\') { i++; continue; }
                    if (c == stringChar) inString = false;
                    continue;
                }

                if (c == '/' && i + 1 < code.Length)
                {
                    if (code[i + 1] == '/') { inSingleComment = true; i++; continue; }
                    if (code[i + 1] == '*') { inMultiComment = true; i++; continue; }
                }

                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                }
            }

            return inSingleComment || inMultiComment || inString;
        }
    }
}
