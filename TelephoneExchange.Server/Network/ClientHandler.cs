using System.Net.Sockets;
using System.Text;
using TelephoneExchange.Server.Models;

namespace TelephoneExchange.Server.Network
{
    /// <summary>
    /// Обработчик клиентского подключения
    /// </summary>
    public class ClientHandler
    {
        public TcpClient TcpClient { get; }
        public NetworkStream NetworkStream { get; }
        public Subscriber? Subscriber { get; set; }
        
        private readonly ATS _ats;
        private bool _isRunning;

        public ClientHandler(TcpClient tcpClient, ATS ats)
        {
            TcpClient = tcpClient;
            NetworkStream = tcpClient.GetStream();
            _ats = ats;
            _isRunning = false;
        }

        /// <summary>
        /// Начать прослушивание команд в отдельном потоке
        /// </summary>
        public void StartListening()
        {
            _isRunning = true;
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[1024];
                    var messageBuilder = new StringBuilder();

                    while (_isRunning && TcpClient.Connected)
                    {
                        int bytesRead = await NetworkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;

                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(data);

                        // Обработка команд, разделенных переводом строки
                        var messages = messageBuilder.ToString().Split('\n');
                        for (int i = 0; i < messages.Length - 1; i++)
                        {
                            var command = messages[i].Trim();
                            if (!string.IsNullOrEmpty(command))
                            {
                                ProcessCommand(command);
                            }
                        }
                        messageBuilder.Clear();
                        messageBuilder.Append(messages[^1]);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка чтения от клиента: {ex.Message}");
                }
                finally
                {
                    Disconnect();
                }
            });
        }

        /// <summary>
        /// Отправить сообщение клиенту
        /// </summary>
        public void SendMessage(string message)
        {
            try
            {
                if (TcpClient.Connected)
                {
                    var data = Encoding.UTF8.GetBytes(message + "\n");
                    NetworkStream.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка отправки сообщения клиенту: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработать команду от клиента
        /// </summary>
        private void ProcessCommand(string command)
        {
            try
            {
                if (command == "CONNECT")
                {
                    _ats.RegisterSubscriber(this);
                }
                else if (command == "PICKUP")
                {
                    if (Subscriber != null)
                        _ats.HandlePickup(Subscriber);
                }
                else if (command == "HANGUP")
                {
                    if (Subscriber != null)
                        _ats.HandleHangup(Subscriber);
                }
                else if (command.StartsWith("DIAL:"))
                {
                    var targetNumber = command.Substring(5);
                    if (Subscriber != null)
                        _ats.HandleDial(Subscriber, targetNumber);
                }
                else if (command.StartsWith("MESSAGE:"))
                {
                    var message = command.Substring(8);
                    if (Subscriber != null)
                        _ats.HandleMessage(Subscriber, message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка обработки команды '{command}': {ex.Message}");
            }
        }

        /// <summary>
        /// Закрыть соединение
        /// </summary>
        public void Disconnect()
        {
            _isRunning = false;
            
            if (Subscriber != null)
            {
                _ats.UnregisterSubscriber(Subscriber);
            }

            try
            {
                NetworkStream?.Close();
                TcpClient?.Close();
            }
            catch { }
        }
    }
}
