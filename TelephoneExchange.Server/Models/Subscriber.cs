using TelephoneExchange.Server.Network;

namespace TelephoneExchange.Server.Models
{
    /// <summary>
    /// Модель абонента на сервере
    /// </summary>
    public class Subscriber
    {
        /// <summary>
        /// Номер телефона абонента
        /// </summary>
        public string PhoneNumber { get; }
        
        /// <summary>
        /// Текущее состояние абонента
        /// </summary>
        public SubscriberState State { get; private set; }
        
        /// <summary>
        /// Текущее соединение (если есть)
        /// </summary>
        public Connection? CurrentConnection { get; set; }
        
        /// <summary>
        /// Обработчик клиентского подключения
        /// </summary>
        public ClientHandler ClientHandler { get; }

        public Subscriber(string phoneNumber, ClientHandler clientHandler)
        {
            PhoneNumber = phoneNumber;
            ClientHandler = clientHandler;
            State = SubscriberState.Idle;
        }

        /// <summary>
        /// Изменить состояние абонента
        /// </summary>
        public void ChangeState(SubscriberState newState)
        {
            State = newState;
        }

        /// <summary>
        /// Проверка возможности совершить вызов
        /// </summary>
        public bool CanMakeCall()
        {
            return State == SubscriberState.Ready || State == SubscriberState.Dialing;
        }

        /// <summary>
        /// Проверка возможности принять вызов
        /// </summary>
        public bool CanReceiveCall()
        {
            return State == SubscriberState.Idle;
        }
    }
}
