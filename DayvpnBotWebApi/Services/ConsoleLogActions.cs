using Telegram.Bot.Types;

namespace DayvpnBotWebApi.Services
{
    public static class ConsoleLogActions
    {
        public static async Task ConsoleLogReceivedMessageAsync(Message message)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8; // فعال کردن UTF-8
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("📩 Message Received From: ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{message.From.FirstName} {message.From.LastName}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("📝 Text: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(message.Text);
            Console.ForegroundColor = originalColor;
        }
    }
}
