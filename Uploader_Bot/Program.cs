using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

string token = "BOT_TOKEN";
var bot = new TelegramBotClient(token);
var cts = new CancellationTokenSource();

var channelId1 = -1234567891234; // Channel ID for movies
var channelId2 = -1987654321987; // Channel ID for links
var channelUsername = "@YourChannelUsername"; // Channel username
long adminId = 0123456789; // Admin ID

Dictionary<long, int> pendingCaptions = new(); // Store pending messages for captions

bot.StartReceiving(HandleUpdateAsync, HandleError, new ReceiverOptions(), cts.Token);
Console.WriteLine("Bot is running...");
Console.ReadLine();

async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken token)
{
    if (update.Type == UpdateType.Message && update.Message != null)
    {
        await HandleMessageAsync(update.Message);
    }
    else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
    {
        await HandleCallbackQueryAsync(update.CallbackQuery);
    }
}

async Task HandleMessageAsync(Message msg)
{
    // Check `/start X` messages
    if (msg.Text != null && msg.Text.StartsWith("/start"))
    {
        string[] parts = msg.Text.Split(' ');
        int messageId = 0;

        if (parts.Length > 1 && int.TryParse(parts[1], out messageId))
        {
            // Check if user is a member of the channel
            ChatMember member = await bot.GetChatMember(channelUsername, msg.Chat.Id);
            if (member.Status == ChatMemberStatus.Member ||
                member.Status == ChatMemberStatus.Administrator ||
                member.Status == ChatMemberStatus.Creator)
            {
                // If member, forward the relevant message from the channel
                await bot.CopyMessage(msg.Chat.Id, channelId1, messageId);
                await bot.CopyMessage(msg.Chat.Id, channelId1, messageId + 1);
            }
            else
            {
                // If not a member, prompt to join the channel with a "Check Membership" button
                await bot.SendMessage(
                    chatId: msg.Chat.Id,
                    text: "❌ You are not a member of the channel, please join first!",
                    replyMarkup: new InlineKeyboardMarkup(new[]
                    {
                        InlineKeyboardButton.WithUrl("📢 Join Channel", "https://t.me/aztestchannelll"),
                        InlineKeyboardButton.WithCallbackData("🔄 Check Membership", $"check_membership?start={messageId}")
                    })
                );
            }
        }
    }
    // Check admin messages (photo or video)
    else if (msg.From.Id == adminId && (msg.Photo != null || msg.Video != null))
    {
        await bot.SendMessage(msg.Chat.Id, "📌 Please send the caption for this content.");
        pendingCaptions[msg.Chat.Id] = msg.MessageId; // Store message waiting for caption
    }
    // Receive caption and forward content
    else if (msg.From.Id == adminId && msg.Text != null && pendingCaptions.ContainsKey(msg.Chat.Id))
    {
        int messageId = pendingCaptions[msg.Chat.Id]; // Get the previous message
        pendingCaptions.Remove(msg.Chat.Id); // Remove from pending list

        // Forward the message to the first channel
        Message sentMessage = await bot.ForwardMessage(
            chatId: channelId1,
            fromChatId: msg.Chat.Id,
            messageId: messageId
        );

        // Send the caption to the first channel
        if (!string.IsNullOrEmpty(msg.Text))
        {
            await bot.SendMessage(
                chatId: channelId1,
                text: msg.Text
            );
        }

        // ✅ Create a link and send to the second channel
        string link = $"https://t.me/AzUploaderAzBot?start={sentMessage.MessageId}";
        await bot.SendMessage(
            channelId2,
            $"📢 {msg.Text}\n🔗 {link}"
        );
    }
    else if (msg.From.Id != adminId && (msg.Photo != null || msg.Video != null))
    {
        await bot.SendMessage(msg.Chat.Id, "❌ You are not the admin!!");
    }
}

async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
{
    string[] dataParts = callbackQuery.Data.Split('?');
    string command = dataParts[0];
    string param = dataParts.Length > 1 ? dataParts[1] : "";

    if (command == "check_membership")
    {
        // Check if the user is a member of the channel
        ChatMember member = await bot.GetChatMember(channelUsername, callbackQuery.From.Id);
        if (member.Status == ChatMemberStatus.Member ||
            member.Status == ChatMemberStatus.Administrator ||
            member.Status == ChatMemberStatus.Creator)
        {
            await bot.AnswerCallbackQuery(callbackQuery.Id, "✅ You are a member!", showAlert: true);

            // Simulate a /start X command
            if (param.StartsWith("start="))
            {
                string fakeStartCommand = $"/start {param.Replace("start=", "")}";
                Message fakeMessage = new()
                {
                    Chat = new Chat { Id = callbackQuery.From.Id },
                    Text = fakeStartCommand,
                    From = new User { Id = callbackQuery.From.Id }
                };
                await HandleMessageAsync(fakeMessage);
            }
        }
        else
        {
            await bot.AnswerCallbackQuery(callbackQuery.Id, "❌ You are not a member yet!", showAlert: true);
        }
    }
}

async Task HandleError(ITelegramBotClient bot, Exception ex, CancellationToken token)
{
    Console.WriteLine($"Error: {ex.Message}");
}
