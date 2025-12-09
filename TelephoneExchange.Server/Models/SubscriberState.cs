using System.ComponentModel;

namespace TelephoneExchange.Server.Models
{
    /// <summary>
    /// Состояния абонента в системе
    /// </summary>
    public enum SubscriberState
    {
        [Description("Ожидание")]
        Idle,

        [Description("Готов")]
        Ready,

        [Description("Занято")]
        Busy,

        [Description("Набор номера")] 
        Dialing,

        [Description("Входящий вызов")] 
        Ringing,

        [Description("В разговоре")] 
        InCall
    }

    public static class EnumExtensions
    {
        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attribute?.Description ?? value.ToString();
        }
    }
}
