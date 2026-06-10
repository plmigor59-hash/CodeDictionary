using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Lusamine.GTranslate;

namespace CodeDictionary.Services
{
    public class TranslationService
    {
        private readonly Translator _translator;

        public TranslationService()
        {
            _translator = new Translator();
        }

        /// <summary>
        /// Переводит выделенный текст
        /// </summary>
        /// <param name="text">Текст для перевода</param>
        /// <param name="targetLanguage">Целевой язык (по умолчанию "ru")</param>
        /// <param name="sourceLanguage">Исходный язык (null = автоопределение)</param>
        /// <returns>Переведенный текст</returns>
        public async Task<string> TranslateTextAsync(string text, string targetLanguage = "ru", string sourceLanguage = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            try
            {
                ITranslated result;

                if (!string.IsNullOrEmpty(sourceLanguage))
                {
                    result = await _translator.TranslateAsync(text, src: sourceLanguage, dest: targetLanguage);
                }
                else
                {
                    // Автоопределение исходного языка
                    result = await _translator.TranslateAsync(text, dest: targetLanguage);
                }

                return result.Text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Translation error: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }
        }

        /// <summary>
        /// Определяет язык текста
        /// </summary>
        public async Task<string> DetectLanguageAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "unknown";

            try
            {
                var detected = await _translator.DetectAsync(text);
                return detected.Lang;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Detection error: {ex.Message}");
                return "unknown";
            }
        }

        /// <summary>
        /// Получает список всех поддерживаемых языков
        /// </summary>
        public List<LanguageInfo> GetAllLanguages()
        {
            var languages = new List<LanguageInfo>();

            foreach (var (code, name) in Languages.All)
            {
                languages.Add(new LanguageInfo { Code = code, Name = name });
            }

            return languages;
        }

        public void Dispose()
        {
            _translator?.Dispose();
        }
    }

    public class LanguageInfo
    {
        public string Code { get; set; }
        public string Name { get; set; }
    }
}
