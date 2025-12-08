using System.Net;
using System.Net.Sockets;
using TelephoneExchange.Server.Network;

namespace TelephoneExchange.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== Мини-АТС Сервер ===");
            
            // Создать экземпляр АТС
            var ats = new MiniATS();
            
            // Загрузить конфигурацию
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            ats.LoadConfig(configPath);
            
            // Запустить TCP сервер
            var port = 5000; // Можно читать из конфигурации
            var listener = new TcpListener(IPAddress.Any, port);
            
            try
            {
                listener.Start();
                Console.WriteLine($"Сервер запущен на порту {port}");
                Console.WriteLine("Ожидание подключений...\n");
                
                // Обработка Ctrl+C для graceful shutdown
                Console.CancelKeyPress += (sender, e) =>
                {
                    e.Cancel = true;
                    Console.WriteLine("\nОстановка сервера...");
                    listener.Stop();
                };
                
                // Принимать подключения
                while (true)
                {
                    try
                    {
                        var client = await listener.AcceptTcpClientAsync();
                        var clientEndPoint = client.Client.RemoteEndPoint;
                        Console.WriteLine($"Подключился клиент {clientEndPoint}");
                        
                        // Создать обработчик для клиента
                        var handler = new ClientHandler(client, ats);
                        handler.StartListening();
                    }
                    catch (Exception ex)
                    {
                        if (listener.Server.IsBound)
                        {
                            Console.WriteLine($"Ошибка при принятии подключения: {ex.Message}");
                        }
                        else
                        {
                            break; // Сервер остановлен
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Критическая ошибка сервера: {ex.Message}");
            }
            finally
            {
                listener.Stop();
                Console.WriteLine("Сервер остановлен");
            }
        }
    }
}
