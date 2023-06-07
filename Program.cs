using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Newtonsoft.Json;
using System.Text;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;


var botClient = new TelegramBotClient("6251175365:AAF2VWCU1zJGYkUz3cjK_NKIsjoolGT57go");
using CancellationTokenSource cts = new();

ReceiverOptions receiverOptions = new()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(
    updateHandler: async (client, update, token) => await HandleUpdateAsync(client, update, token),
    pollingErrorHandler: async (client, exception, token) => await HandlePollingErrorAsync(client, exception, token),
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

var me = await botClient.GetMeAsync();

Console.WriteLine($"Start listening for @{me.Username}");
Console.ReadLine();

cts.Cancel();

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.Message is not { } message)
        return;

    if (message.Text is not { } messageText)
        return;

    var chatId = message.Chat.Id;

    Console.WriteLine($"Received a '{messageText}' message in chat {chatId}.");

    var parts = messageText.Split(' ', 2);

    if (parts.Length < 2)
    {
        if (parts.Length < 1)
        {
            Console.WriteLine("Invalid command format");
            return;
        }

        var command = parts[0];

        switch (command)
        {
            case "/about":
                await HandleAboutCommand(botClient, chatId);
                break;
            case "/start":
                await HandleStartCommand(botClient, chatId);
                break;
            case "/quickrecc":
                await HandleQuickRecommendationCommand(botClient, chatId);
                break;
            default:
                Console.WriteLine("Invalid command");
                break;
        }
    }
    else
    {
        var command = parts[0];
        var music_variable = parts[1];

        switch (command)
        {
            case "/artist":
                await Near(botClient, chatId, music_variable);
                break;
            case "/genres":
                await GetPerformersByGenre(botClient, chatId, music_variable);
                break;
            case "/city":
                await GetEventsByLocation(botClient, chatId, music_variable);
                break;
            case "/input_genres":
                var genres = music_variable;
                await SaveGenresToTable( botClient, chatId, genres);
                break;
            case "/update_genres":
                await UpdateGenresInTable(botClient, chatId, music_variable);
                break;
            default:
                Console.WriteLine("Invalid command");
                break;
        }
    }
}
async Task HandleAboutCommand(ITelegramBotClient botClient, long chatId)
{
    var explanain = "Отже ти бажаєш наповнити свою голівоньку знаннями, солаго 😉\n\n" +
        "/artist {ім'я артиста} - Шанс того, що ти знайдеш концерти залежить від того, чи правильно ти напишеш ім'я, просто напиши через пробіл від команди\n" +
        "/genres {назва жанру} - Доступні жанри для пошуку: rock♦folk♦classical♦hard rock♦soul♦classic rock♦pop♦latin♦hip-hop♦hard rock♦jazz♦funk♦indie♦blues♦rap♦techno♦country♦alternative♦blues♦reggae♦electronic♦punk♦rnb\n" +
        "/city {назва міста} - Шанс того, що ти знайдеш концерти залежить від того, чи правильно ти напишеш назву міста, просто напиши через пробіл від команди\n" +
        "/input_genres {жанри} - Тут вибір жанрів ще ширший, можеш ввести навіть ДЕКІЛЬКА жанрів, головне щоб жанр справді існував\n" +
        "/update_genres {жанри} - Якщо тобі осточортів старий жанр, ти завжди можеш його оновити\n" +
        "/quickrecc - Просто тиснеш і все тобі дасть, якщо твій жанр справді можна вважати жанром\n" +
        "І не забувай, що ім'я виконавців, жанрів та міст варто писати англійською. Щасти в пошуках!!! Ахахахаха!!!";
    await botClient.SendTextMessageAsync(chatId, explanain);
}

async Task HandleStartCommand(ITelegramBotClient botClient, long chatId)
{
    var explanation = "Агарр! Ласкаво просимо до світу музичних пригод, мій друже! Я - Сер Мюзік Бот, безстрашний капітан, який веде тебе через штормові води музичних подій та рекомендацій 😉\n\nТут ти знайдеш скарби інформації про виконавців, жанри, музичні події, та отримуватимеш швидкі рекомендації!\n\nМоя картотека складається з наступних команд:\n\n" +
        "/about - Тут ти відкриєш поглиблену інструкцію до користування\n" +
        "/artist {ім'я артиста} - Отримати інформацію про виконавців та майбутні концерти\n" +
        "/genres {назва жанру} - Спробуй відшукати щось новеньке для себе базуючись на жанрі\n" +
        "/city {назва міста} - Знайти музичні події у заданому місті\n" +
        "/input_genres {жанри} - Зберегти улюблені жанри для отримання особливих рекомендацій\n" +
        "/update_genres - Оновити жанри\n" +
        "/quickrecc - Отримати миттєвий порадник для знаходження справжнього скарбу\n"+
        "І не забувай, що ім'я виконавців, жанрів та міст варто писати англійською, бо наш навігатор трохи іноземець та інакше не втумкає. Щасти в пошуках!!! Ахахахаха!!!";

    await botClient.SendTextMessageAsync(chatId, explanation);
}

async Task HandleQuickRecommendationCommand(ITelegramBotClient botClient, long chatId)
{
    try
    {
        var genres = await GetGenresByChatId(chatId);

        if (genres == null)
        {
            await botClient.SendTextMessageAsync(chatId, "Солаги нічого не знайшли. Запишись до реєстру отримувасів рекомендацій.");
            return;
        }

        var recommendation = await GetMusicRecommendation(genres, chatId);

        await botClient.SendTextMessageAsync(chatId, recommendation);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Повна лажа: {ex.Message}");
        await botClient.SendTextMessageAsync(chatId, "Солаги облажалися.");
    }
}
async Task<string> GetGenresByChatId(long chatId)
{
    var clientId = "635943519656-2v3tguccpfp94m9e6i08p3dkm85gn8cs.apps.googleusercontent.com";
    var clientSecret = "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";
    var spreadsheetId = "1OUpf4eeM3iF1Rkm7EhP5EyXKOBsbkXVR6kzdGAD3N-E";
    var range = "Storegen!A:B";
    var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        },
        new[] { SheetsService.Scope.Spreadsheets },
        "user",
        CancellationToken.None);

    var service = new SheetsService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credential,
        ApplicationName = "Storegen",
    });

    var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
    var response = await request.ExecuteAsync();

    var values = response.Values;

    if (values != null && values.Count > 0)
    {
        foreach (var row in values)
        {
            var rowChatId = row[0].ToString();
            if (rowChatId == chatId.ToString())
            {
                var genres = row[1].ToString();
                return genres;
            }
        }
    }

    return null;
}


async Task<string> GetMusicRecommendation(string genres, long chatId)
{
    var apiUrl = "https://localhost:7094/api/superfinder/Recommendation";

    var random = new Random();
    var genreList = genres.Split(','); 
    var randomGenre = genreList[random.Next(genreList.Length)];

    var request = new RecommendationRequest
    {
        ChatId = chatId,
        Genre = randomGenre
    };

    using (var httpClient = new HttpClient())
    {
        var json = JsonConvert.SerializeObject(request);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync(apiUrl, httpContent);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var recommendationResponse = JsonConvert.DeserializeObject<RecommendationResponse>(content);
        return recommendationResponse.Recommendation;
    }
}

static async Task SaveGenresToTable(ITelegramBotClient botClient, long chatId, string genres)
{
    var clientId = "635943519656-2v3tguccpfp94m9e6i08p3dkm85gn8cs.apps.googleusercontent.com";
    var clientSecret = "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";
    var spreadsheetId = "1OUpf4eeM3iF1Rkm7EhP5EyXKOBsbkXVR6kzdGAD3N-E";
    var range = "Storegen!A:B";

    var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        },
        new[] { SheetsService.Scope.Spreadsheets },
        "user",
        CancellationToken.None);

    var service = new SheetsService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credential,
        ApplicationName = "Storegen",
    });

    var values = new List<IList<object>>
    {
        new List<object> { chatId, genres }
    };

    var requestBody = new ValueRange
    {
        Values = values
    };

    try
    {
        var appendRequest = service.Spreadsheets.Values.Append(requestBody, spreadsheetId, range);
        appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
        var appendResponse = await appendRequest.ExecuteAsync();

        if (appendResponse.Updates != null && appendResponse.Updates.UpdatedRows > 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Блискавична перемога!");
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Солаги облажалися.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error saving genres: {ex.Message}");
        await botClient.SendTextMessageAsync(chatId, "Солаги супер налажали.");
    }
}
static async Task UpdateGenresInTable(ITelegramBotClient botClient, long chatId, string newGenres)
{
    var clientId = "635943519656-2v3tguccpfp94m9e6i08p3dkm85gn8cs.apps.googleusercontent.com";
    var clientSecret = "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";
    var spreadsheetId = "1OUpf4eeM3iF1Rkm7EhP5EyXKOBsbkXVR6kzdGAD3N-E";

    var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
        new ClientSecrets
        {
            ClientId = clientId,
            ClientSecret = clientSecret
        },
        new[] { SheetsService.Scope.Spreadsheets },
        "user",
        CancellationToken.None);

    var service = new SheetsService(new BaseClientService.Initializer()
    {
        HttpClientInitializer = credential,
        ApplicationName = "Storegen",
    });

    var range = "Storegen!A:B";

    try
    {
        // Retrieve existing data from the spreadsheet
        var getRange = service.Spreadsheets.Values.Get(spreadsheetId, range);
        var getResponse = await getRange.ExecuteAsync();
        var existingValues = getResponse.Values;

        // Find the row index where the chatId matches
        var rowIndex = -1;
        for (var i = 0; i < existingValues.Count; i++)
        {
            var row = existingValues[i];
            if (row.Count >= 2 && row[0].ToString() == chatId.ToString())
            {
                rowIndex = i;
                break;
            }
        }

        if (rowIndex == -1)
        {
            await botClient.SendTextMessageAsync(chatId, "Вас не знайдено в реєстрі.");
            return;
        }

        // Update the value in column B with new genres
        existingValues[rowIndex][1] = newGenres;

        var updateRange = $"Storegen!B{rowIndex + 1}";

        var valueRange = new ValueRange
        {
            Values = new List<IList<object>> { new List<object> { newGenres } }
        };

        var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, updateRange);
        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;

        var updateResponse = await updateRequest.ExecuteAsync();

        if (updateResponse.UpdatedRows > 0)
        {
            await botClient.SendTextMessageAsync(chatId, "Реєстр оновлено успішно!");
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Солаги облажались.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Лажа: {ex.Message}");
        await botClient.SendTextMessageAsync(chatId, "Провал місії солаг.");
    }
}
async Task Near(ITelegramBotClient botClient, long chatId, string music_variable)
{
    Console.WriteLine($"'/artist' selected with location: {music_variable}");

    await botClient.SendTextMessageAsync(chatId, "Солаги шукають...");

    using (var httpClient = new HttpClient())
    {
        var response = await httpClient.GetAsync($"https://sirmusicbot.azurewebsites.net/api/superfinder/Event_by_Artist/artist/{music_variable}");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Parse and process the content returned from the API
            var events = JsonConvert.DeserializeObject<List<Event>>(content);

            if (events.Count > 0)
            {
                int limit = Math.Min(6, events.Count); 

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < limit; i++)
                {
                    var evt = events[i];
                    sb.AppendLine("Event: " + evt.Title);
                    sb.AppendLine("Date: " + evt.Datetime.ToLocalTime());
                    sb.AppendLine("URL: " + evt.Url);
                    sb.AppendLine();
                }

                string formattedContent = sb.ToString();

                // Send the formatted content back to the user
                await botClient.SendTextMessageAsync(chatId, "Твої події:\n" + formattedContent);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Солаги не знайшли.");
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Солаги облажалися.");
        }
    }
}
async Task GetPerformersByGenre(ITelegramBotClient botClient, long chatId, string music_variable)
{
    Console.WriteLine($"'/genres' selected with genre: {music_variable}");

    await botClient.SendTextMessageAsync(chatId, "Солаги шукають...");

    using (var httpClient = new HttpClient())
    {
        var response = await httpClient.GetAsync($"https://sirmusicbot.azurewebsites.net/genre/{music_variable}");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Parse and process the content returned from the API
            var seatGeekResponse = JsonConvert.DeserializeObject<SeatGeekResponse>(content);

            if (seatGeekResponse.Performers != null && seatGeekResponse.Performers.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var performer in seatGeekResponse.Performers)
                {
                    sb.AppendLine("Performer: " + performer.Name);
                    sb.AppendLine("Type: " + performer.Type);
                    sb.AppendLine("URL: " + performer.Url);
                    sb.AppendLine();
                }

                string formattedContent = sb.ToString();

                // Send the formatted content back to the user
                await botClient.SendTextMessageAsync(chatId, "Твої події:\n" + formattedContent);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Солаги не знайшли.");
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Солаги облажалися.");
        }
    }
}
async Task GetEventsByLocation(ITelegramBotClient botClient, long chatId, string music_variable)
{
    Console.WriteLine($"'/city' selected with location: {music_variable}");

    await botClient.SendTextMessageAsync(chatId, "Солаги шукають...");

    using (var httpClient = new HttpClient())
    {
        var response = await httpClient.GetAsync($"https://sirmusicbot.azurewebsites.net/api/Event_by_Location/city/{music_variable}");
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Parse and process the content returned from the API
            var seatGeekResponse = JsonConvert.DeserializeObject<SeatGeekResponse>(content);

            // Filter the events by music events
            var musicEvents = seatGeekResponse.Events;

            if (musicEvents.Count > 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var evt in musicEvents)
                {
                    sb.AppendLine("Event: " + evt.Title);
                    sb.AppendLine("Date: " + evt.DateTimeLocal.ToLocalTime());
                    sb.AppendLine("URL: " + evt.Url);
                    sb.AppendLine();
                }

                string formattedContent = sb.ToString();

                // Send the formatted content back to the user
                await botClient.SendTextMessageAsync(chatId, "Твої події:\n" + formattedContent);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Солаги не знайшли.");
            }
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Солаги облажалися.");
        }
    }
}

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
{
    var ErrorMessage = exception switch
    {
        ApiRequestException apiRequestException
            => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
        _ => exception.ToString()
    };

    Console.WriteLine(ErrorMessage);
    return Task.CompletedTask;
}
public class SeatGeekResponse
{
    [JsonProperty("events")]
    public List<SeatGeekEvent> Events { get; set; }
    [JsonProperty("performers")]
    public List<SeatGeekPerformer> Performers { get; set; }
}
public class SeatGeekEvent
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("datetime_local")]
    public DateTime DateTimeLocal { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("performers")]
    public List<SeatGeekPerformer> Performers { get; set; }
}
public class Event
{
    [JsonProperty("title")]
    public string Title { get; set; }

    [JsonProperty("datetime")]
    public DateTime Datetime { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }
}
public class SeatGeekPerformer
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("performers")]
    public List<SeatGeekPerformer> Performers { get; set; }
}
public class RecommendationRequest
{
    [JsonProperty("chatId")]
    public long ChatId { get; set; }
    public string Genre { get; set; }
}
public class RecommendationResponse
{
    [JsonProperty("recommendation")]
    public string Recommendation { get; set; }
}