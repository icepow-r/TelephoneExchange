namespace TelephoneExchange.Server.Models
{
    /// <summary>
    /// Состояния абонента в системе
    /// </summary>
    public enum SubscriberState
    {
        /// <summary>
        /// Ожидание (трубка положена)
        /// </summary>
        Idle,
        
        /// <summary>
        /// Готов к набору (трубка снята, есть свободные линии)
        /// </summary>
        Ready,
        
        /// <summary>
        /// Занято (трубка снята, нет свободных линий)
        /// </summary>
        Busy,
        
        /// <summary>
        /// Набор номера (трубка снята, набирает номер)
        /// </summary>
        Dialing,
        
        /// <summary>
        /// Входящий вызов (кто-то звонит этому абоненту)
        /// </summary>
        Ringing,
        
        /// <summary>
        /// В разговоре (соединение установлено)
        /// </summary>
        InCall
    }
}
