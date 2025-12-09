using System.Text.Json.Serialization;

namespace TelephoneExchange.Client.Models
{
    /// <summary>
    /// Информация об абоненте для отображения в списке
    /// </summary>
    public class SubscriberInfo
    {
        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;
        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"Номер: {Number} - Состояние: {State}";
        }
    }
}
