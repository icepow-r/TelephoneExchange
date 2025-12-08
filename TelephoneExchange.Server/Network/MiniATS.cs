using System.Text.Json;
using TelephoneExchange.Server.Models;

namespace TelephoneExchange.Server.Network
{
    /// <summary>
    /// Ядро системы мини-АТС
    /// </summary>
    public class MiniATS
    {
        public int MaxConnections { get; private set; }
        public int PhoneNumberLength { get; private set; }
        
        public int ServerPort { get; private set; }
        
        private readonly List<Subscriber> _subscribers = new();
        private readonly List<Connection> _activeConnections = new();
        private int _nextPhoneNumber = 1;
        private readonly object _lock = new();

        /// <summary>
        /// Загрузить конфигурацию из JSON файла
        /// </summary>
        public void LoadConfig(string configPath)
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<ServerConfig>(json);
                
                if (config != null)
                {
                    MaxConnections = config.MaxConnections;
                    PhoneNumberLength = config.PhoneNumberLength;
                    ServerPort = config.ServerPort;
                    Console.WriteLine($"Конфигурация загружена: MaxConnections={MaxConnections}, PhoneNumberLength={PhoneNumberLength}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки конфигурации: {ex.Message}");
                // Значения по умолчанию
                MaxConnections = 4;
                PhoneNumberLength = 3;
            }
        }

        /// <summary>
        /// Сгенерировать уникальный номер телефона
        /// </summary>
        private string GeneratePhoneNumber()
        {
            lock (_lock)
            {
                var number = _nextPhoneNumber.ToString().PadLeft(PhoneNumberLength, '0');
                _nextPhoneNumber++;
                return number;
            }
        }

        /// <summary>
        /// Зарегистрировать нового абонента
        /// </summary>
        public void RegisterSubscriber(ClientHandler handler)
        {
            lock (_lock)
            {
                var phoneNumber = GeneratePhoneNumber();
                var subscriber = new Subscriber(phoneNumber, handler);
                handler.Subscriber = subscriber;
                _subscribers.Add(subscriber);
                
                handler.SendMessage($"ASSIGNED:{phoneNumber}");
                handler.SendMessage($"STATE:{subscriber.State}");
                
                Console.WriteLine($"Абоненту присвоен номер {phoneNumber}");
                
                BroadcastSubscribersList();
            }
        }

        /// <summary>
        /// Отключить абонента
        /// </summary>
        public void UnregisterSubscriber(Subscriber subscriber)
        {
            lock (_lock)
            {
                // Разорвать активное соединение, если есть
                if (subscriber.CurrentConnection != null)
                {
                    RemoveConnection(subscriber.CurrentConnection);
                }
                
                _subscribers.Remove(subscriber);
                Console.WriteLine($"Клиент отключился {subscriber.PhoneNumber}");
                
                BroadcastSubscribersList();
            }
        }

        /// <summary>
        /// Обработать снятие трубки
        /// </summary>
        public void HandlePickup(Subscriber subscriber)
        {
            lock (_lock)
            {
                if (subscriber.State != SubscriberState.Idle && subscriber.State != SubscriberState.Ringing)
                {
                    return;
                }

                // Если входящий вызов - принять его
                if (subscriber.State == SubscriberState.Ringing)
                {
                    // Найти соединение, где этот абонент является вызываемым
                    var caller = _subscribers.FirstOrDefault(s => 
                        s.CurrentConnection != null && 
                        s.CurrentConnection.GetOtherSubscriber(s) == subscriber);
                    
                    if (caller != null && caller.CurrentConnection != null)
                    {
                        // Установить соединение
                        subscriber.ChangeState(SubscriberState.InCall);
                        caller.ChangeState(SubscriberState.InCall);
                        
                        subscriber.ClientHandler.SendMessage("CALL_CONNECTED");
                        subscriber.ClientHandler.SendMessage($"STATE:{subscriber.State}");
                        
                        caller.ClientHandler.SendMessage("CALL_CONNECTED");
                        caller.ClientHandler.SendMessage($"STATE:{caller.State}");
                        
                        Console.WriteLine($"Соединение установлено: {caller.PhoneNumber} <-> {subscriber.PhoneNumber}");
                        
                        BroadcastSubscribersList();
                    }
                    return;
                }

                // Проверить лимит соединений
                if (!CanCreateConnection())
                {
                    subscriber.ChangeState(SubscriberState.Busy);
                    subscriber.ClientHandler.SendMessage("SIGNAL:занято");
                    subscriber.ClientHandler.SendMessage($"STATE:{subscriber.State}");
                }
                else
                {
                    subscriber.ChangeState(SubscriberState.Ready);
                    subscriber.ClientHandler.SendMessage("SIGNAL:готов");
                    subscriber.ClientHandler.SendMessage($"STATE:{subscriber.State}");
                }
                
                BroadcastSubscribersList();
            }
        }

        /// <summary>
        /// Обработать набор номера
        /// </summary>
        public void HandleDial(Subscriber caller, string targetNumber)
        {
            lock (_lock)
            {
                if (caller.State != SubscriberState.Ready)
                {
                    caller.ClientHandler.SendMessage("ERROR:Невозможно совершить вызов в текущем состоянии");
                    return;
                }

                // Найти вызываемого абонента
                var target = _subscribers.FirstOrDefault(s => s.PhoneNumber == targetNumber);
                
                if (target == null)
                {
                    caller.ClientHandler.SendMessage("ERROR:Номер не найден");
                    return;
                }

                if (!target.CanReceiveCall())
                {
                    caller.ClientHandler.SendMessage("ERROR:Абонент занят");
                    return;
                }

                // Создать соединение
                CreateConnection(caller, target);
            }
        }

        /// <summary>
        /// Обработать положение трубки
        /// </summary>
        public void HandleHangup(Subscriber subscriber)
        {
            lock (_lock)
            {
                if (subscriber.CurrentConnection != null)
                {
                    RemoveConnection(subscriber.CurrentConnection);
                }
                else
                {
                    subscriber.ChangeState(SubscriberState.Idle);
                    subscriber.ClientHandler.SendMessage($"STATE:{subscriber.State}");
                    BroadcastSubscribersList();
                }
            }
        }

        /// <summary>
        /// Обработать сообщение
        /// </summary>
        public void HandleMessage(Subscriber sender, string message)
        {
            lock (_lock)
            {
                if (sender.CurrentConnection != null && sender.State == SubscriberState.InCall)
                {
                    sender.CurrentConnection.SendMessage(sender, message);
                }
            }
        }

        /// <summary>
        /// Проверка доступности линий
        /// </summary>
        private bool CanCreateConnection()
        {
            return _activeConnections.Count < MaxConnections;
        }

        /// <summary>
        /// Создать соединение
        /// </summary>
        private void CreateConnection(Subscriber caller, Subscriber target)
        {
            var connection = new Connection(caller, target);
            _activeConnections.Add(connection);
            
            caller.CurrentConnection = connection;
            target.CurrentConnection = connection;
            
            caller.ChangeState(SubscriberState.Dialing);
            target.ChangeState(SubscriberState.Ringing);
            
            caller.ClientHandler.SendMessage($"STATE:{caller.State}");
            target.ClientHandler.SendMessage($"INCOMING_CALL:{caller.PhoneNumber}");
            target.ClientHandler.SendMessage($"STATE:{target.State}");
            
            Console.WriteLine($"Вызов: {caller.PhoneNumber} -> {target.PhoneNumber}");
            
            BroadcastSubscribersList();
        }

        /// <summary>
        /// Удалить соединение
        /// </summary>
        private void RemoveConnection(Connection connection)
        {
            connection.Disconnect();
            _activeConnections.Remove(connection);
            
            connection.SubscriberA.ClientHandler.SendMessage("CALL_ENDED");
            connection.SubscriberA.ClientHandler.SendMessage($"STATE:{connection.SubscriberA.State}");
            
            connection.SubscriberB.ClientHandler.SendMessage("CALL_ENDED");
            connection.SubscriberB.ClientHandler.SendMessage($"STATE:{connection.SubscriberB.State}");
            
            Console.WriteLine($"Соединение разорвано: {connection.SubscriberA.PhoneNumber} <-> {connection.SubscriberB.PhoneNumber}");
            
            BroadcastSubscribersList();
        }

        /// <summary>
        /// Отправить всем клиентам список абонентов и их состояний
        /// </summary>
        private void BroadcastSubscribersList()
        {
            var subscribersList = _subscribers.Select(s => new
            {
                number = s.PhoneNumber,
                state = s.State.ToString()
            }).ToList();
            
            var json = JsonSerializer.Serialize(subscribersList);
            var message = $"SUBSCRIBERS:{json}";
            
            foreach (var subscriber in _subscribers)
            {
                subscriber.ClientHandler.SendMessage(message);
            }
        }
    }

    /// <summary>
    /// Конфигурация сервера
    /// </summary>
    public class ServerConfig
    {
        public int MaxConnections { get; set; }
        public int PhoneNumberLength { get; set; }
        public int ServerPort { get; set; }
    }
}
