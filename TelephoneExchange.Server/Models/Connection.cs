namespace TelephoneExchange.Server.Models
{
    /// <summary>
    /// Соединение между двумя абонентами
    /// </summary>
    public class Connection
    {
        /// <summary>
        /// Первый абонент
        /// </summary>
        public Subscriber SubscriberA { get; }
        
        /// <summary>
        /// Второй абонент
        /// </summary>
        public Subscriber SubscriberB { get; }
        
        /// <summary>
        /// Время установления соединения
        /// </summary>
        public DateTime EstablishedAt { get; }

        public Connection(Subscriber subscriberA, Subscriber subscriberB)
        {
            SubscriberA = subscriberA;
            SubscriberB = subscriberB;
            EstablishedAt = DateTime.Now;
        }

        /// <summary>
        /// Отправить сообщение от одного абонента другому
        /// </summary>
        public void SendMessage(Subscriber from, string message)
        {
            var recipient = GetOtherSubscriber(from);
            if (recipient != null)
            {
                recipient.ClientHandler.SendMessage($"MESSAGE:{message}");
            }
        }

        /// <summary>
        /// Получить второго участника соединения
        /// </summary>
        public Subscriber? GetOtherSubscriber(Subscriber subscriber)
        {
            if (subscriber == SubscriberA)
                return SubscriberB;
            if (subscriber == SubscriberB)
                return SubscriberA;
            return null;
        }

        /// <summary>
        /// Разорвать соединение
        /// </summary>
        public void Disconnect()
        {
            SubscriberA.ChangeState(SubscriberState.Idle);
            SubscriberB.ChangeState(SubscriberState.Idle);
            SubscriberA.CurrentConnection = null;
            SubscriberB.CurrentConnection = null;
        }
    }
}
