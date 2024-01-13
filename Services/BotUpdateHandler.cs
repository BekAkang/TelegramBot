using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.OpenApi.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
#pragma warning disable 

namespace Exam.Services;

public class BotUpdateHandler : IUpdateHandler
{
    const string chat_id = "-1002024296076";
    private readonly ILogger<BotUpdateHandler> _logger;
    private readonly IServiceProvider _serviceProvider;
    private Student? student;
    public BotUpdateHandler(ILogger<BotUpdateHandler> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }
    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Polling error");
        return Task.CompletedTask;
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {

                _logger.LogInformation("Update received from UserId: {0}", update.Message!.Chat.Id);
                _logger.LogInformation("Update received from UserName: {0}", update.Message!.Chat.Username);
                _logger.LogInformation("Updated body: {0}", update.Message!.Text);

                if (update.Message!.Text == "/start")
                    {
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, "  Assalomu Aleykum Botga Xush kelibsiz\nImtihon natijasini bilish uchun O'zingizga tegishli bo'lgan ID ni kiriting. ", cancellationToken: cancellationToken);
                        return;
                    }

                    if(update.Type == UpdateType.Message)
                    {
                        var messageText = update.Message!.Text;
                        
                        // try to parse message to long type
                        if(long.TryParse(messageText, out var studentId) && messageText.Length == 8)
                        {
                            //await botClient.SendTextMessageAsync(update.Message.Chat.Id, "Ismingizni kiriting ", cancellationToken: cancellationToken);
                            student = await GetStudent(studentId);
                            if(student == null)
                            {
                                await botClient.SendTextMessageAsync(update.Message.Chat.Id, "Sizning ID raqamingiz noto'g'ri kiritilgan.\nIltimos Qaytadan ID raqamingizni kiriting", cancellationToken: cancellationToken);
                                return;
                            }
                            else
                            {
                                var message =  await botClient.SendTextMessageAsync(update.Message.Chat.Id, "Iltimos kuting ... ", cancellationToken: cancellationToken);
                                var messageId = message.MessageId;
                                using (var photoStream = new FileStream(student.photoPath!, FileMode.Open))
                                {
                                        var photo = new InputOnlineFile(photoStream);
                                        await botClient.DeleteMessageAsync(update.Message.Chat.Id, messageId, cancellationToken: cancellationToken);
                                        await botClient.SendPhotoAsync(update.Message.Chat.Id, photo, student.StudentString, cancellationToken: cancellationToken);
                                }
                                //SaveToCache(update.Message.Chat.Id);
                                return;
                            }
                        }
                        
                        var chatId = await GetFromCache(update.Message.Chat.Id);
                        if(chatId != 0)
                        {
                            _logger.LogInformation(update.Message.Text);
                            if(student != null && student.Name.ToUpper() == update.Message.Text.ToUpper())
                            {
                              _logger.LogInformation(student.Name);

                              var message =  await botClient.SendTextMessageAsync(chatId, "Iltimos kutib ... ", cancellationToken: cancellationToken);
                              var messageId = message.MessageId;
                              using (var photoStream = new FileStream(student.photoPath!, FileMode.Open))
                                {
                                        var photo = new InputOnlineFile(photoStream);
                                        await botClient.DeleteMessageAsync(update.Message.Chat.Id, messageId, cancellationToken: cancellationToken);
                                        await botClient.SendPhotoAsync(chatId, photo, student.StudentString, cancellationToken: cancellationToken);
                                }
                                DeleteFromCache(update.Message.Chat.Id);
                                return;
                            }
                            else
                            {
                                await botClient.SendTextMessageAsync(chatId, "Sizning ID raqamingiz yoki ismingiz noto'g'ri kiritilgan.\nIltimos Qaytadan ID raqamingizni kiriting", cancellationToken: cancellationToken);
                                DeleteFromCache(update.Message.Chat.Id);
                                return;
                            }
                        }
                        
                        await botClient.SendTextMessageAsync(update.Message.Chat.Id, "ID ni to'g'ri formatda kiriting\nMisol uchun:00000000", cancellationToken: cancellationToken);   
                        
                    }
        }
        catch (System.Exception)
        {
            
            throw new System.Exception("Error");
        }
    }
   
    public async Task<Student> GetStudent(long studentId)
    {
        try
        {
            _logger.LogInformation("GetStudent");
            var _memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var data =  _memoryCache.Get<List<Student>>("student");
            if(data != null)
            {
                var item = data.FirstOrDefault(x => x.StudentId == studentId);
                if(item != null)
                {
                    return item;
                }
            }

            var jsonData = await System.IO.File.ReadAllBytesAsync("JsonData.json");
            var secretData = JsonSerializer.Deserialize<List<Student>>(jsonData);
            _memoryCache.Set("student", secretData, TimeSpan.FromDays(1));

            var item2 = secretData!.FirstOrDefault(x => x.StudentId == studentId);
            if(item2 != null)
            {
                return item2;
            }

            return null!;
        }
        catch (System.Exception e)
        {
            _logger.LogError(e.Message, "GetStudent");
            throw new System.Exception(e.Message);
        }
    }

    public async Task SaveToCache(long chatId)
    {
        try
        {
            _logger.LogInformation("SaveToCache");
            var _memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var oldChatId =  _memoryCache.Get<string>(chatId);

            if(oldChatId != null)
            {
                _memoryCache.Remove(oldChatId);
            }
            
            _memoryCache.Set(chatId, chatId, TimeSpan.FromDays(1));
        }
        catch (System.Exception e)
        {
            _logger.LogError(e.Message, "SaveToCache");
            throw new System.Exception(e.Message);
        }
    }

    public async Task<long> GetFromCache(long chatId)
    {
        try
        {
            _logger.LogInformation("GetFromCache");
            var _memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var data =  _memoryCache.Get<long>(chatId);
            return data!;
        }
        catch (System.Exception e)
        {
            _logger.LogError(e.Message, "GetFromCache");
            throw new System.Exception(e.Message);
        }
    }

    public Task DeleteFromCache(long chatId)
    {
        try
        {
            _logger.LogInformation("DeleteFromCache");
            var _memoryCache = _serviceProvider.GetRequiredService<IMemoryCache>();
            _memoryCache.Remove(chatId);
            return Task.CompletedTask;
        }
        catch (System.Exception e)
        {
            _logger.LogError(e.Message, "DeleteFromCache");
            throw new System.Exception(e.Message);
        }
    }
}