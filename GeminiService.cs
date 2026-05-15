using System.Text;
using System.Text.Json;

namespace Project
{
    public class GeminiService
    {
        private const string ApiKey = "AIzaSyDfAqpZ83V836Zb3Ba_jHO92gNabInAMB8";
        private const string Url =
            "https://generativelanguage.googleapis.com/v1beta/models/" +
            "gemini-1.5-flash:generateContent?key=" + ApiKey;

        private readonly HttpClient _http = new();

        private static readonly JsonSerializerOptions _opts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        // ─── הפרומט הקבוע שמסביר לGemini את חוקי המשחק ───
        private const string SystemPrompt = @"
אתה שחקן במשחק צוללות (Battleship).
חוקי המשחק:
- הלוח הוא 10x10 (שורות 0-9, עמודות 0-9)
- אתה צריך לירות על לוח היריב ולמצוא ולהטביע את כל הצוללות שלו
- יש 5 צוללות: אורך 4, אורך 3, אורך 3, אורך 2, אורך 2
- כל תור עליך לבחור תא שעדיין לא ירו עליו

מצב הלוח שיישלח אליך:
- 0 = לא ירו על תא זה (זמין לירייה)
- 1 = פספוס
- 2 = פגיעה
- 3 = צוללת טבועה

אסטרטגיה:
- עדיף לירות על תאים שלא ירו עליהם
- אם פגעת - ירה על תאים סמוכים
- חשוב: תחזיר תשובה בJSON בלבד ללא טקסט נוסף

פורמט התשובה חייב להיות בדיוק:
{""row"": X, ""col"": Y}
כאשר X ו-Y הם מספרים בין 0 ל-9.
";

        /// <summary>
        /// מקבל את מצב הלוח ומחזיר את הירייה הבאה של Gemini.
        /// </summary>
        public async Task<(int row, int col)?> GetNextShotAsync(int[,] board)
        {
            try
            {
                // בניית תיאור הלוח
                var boardDescription = BuildBoardJson(board);

                string prompt = SystemPrompt + "\n\nמצב הלוח הנוכחי:\n" + boardDescription +
                    "\n\nבחר את הירייה הבאה. החזר JSON בלבד בפורמט: {\"row\": X, \"col\": Y}";

                // בניית הבקשה ל-Gemini
                var requestBody = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[]
                            {
                                new { text = prompt }
                            }
                        }
                    },
                    generationConfig = new
                    {
                        temperature = 0.3,  // נמוך = יותר עקבי
                        maxOutputTokens = 50 // רק JSON קצר
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _http.PostAsync(Url, content);
                var responseJson = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return null;

                // חילוץ הטקסט מהתשובה
                var doc = JsonDocument.Parse(responseJson);
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (string.IsNullOrEmpty(text))
                    return null;

                return ParseResponse(text);
            }
            catch
            {
                return null; // במקרה של שגיאה
            }
        }

        /// <summary>
        /// בונה JSON של מצב הלוח.
        /// 0=פנוי, 1=פספוס, 2=פגיעה, 3=טבוע
        /// </summary>
        private string BuildBoardJson(int[,] board)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{\"board\": [");

            for (int r = 0; r < 10; r++)
            {
                sb.Append("  [");
                for (int c = 0; c < 10; c++)
                {
                    sb.Append(board[r, c]);
                    if (c < 9) sb.Append(",");
                }
                sb.Append("]");
                if (r < 9) sb.Append(",");
                sb.AppendLine();
            }

            sb.AppendLine("]}");
            return sb.ToString();
        }

        /// <summary>
        /// מפרסר את תשובת Gemini ומחלץ row ו-col.
        /// </summary>
        private (int row, int col)? ParseResponse(string text)
        {
            try
            {
                // נקה את הטקסט - הסר ```json ``` אם יש
                text = text.Trim();
                if (text.StartsWith("```"))
                {
                    text = text.Replace("```json", "").Replace("```", "").Trim();
                }

                var doc = JsonDocument.Parse(text);
                int row = doc.RootElement.GetProperty("row").GetInt32();
                int col = doc.RootElement.GetProperty("col").GetInt32();

                // וידוא שהערכים בטווח תקין
                if (row < 0 || row > 9 || col < 0 || col > 9)
                    return null;

                return (row, col);
            }
            catch
            {
                return null;
            }
        }
    }
}