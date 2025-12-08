namespace TelephoneExchange.Client.Models
{
    /// <summary>
    /// Информация об абоненте для отображения в списке
    /// </summary>
    public class SubscriberInfo
    {
        public string Number { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"Номер: {Number} - Состояние: {State}";
        }
    }
}
