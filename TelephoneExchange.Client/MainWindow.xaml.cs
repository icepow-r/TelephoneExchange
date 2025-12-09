using System.Windows;
using TelephoneExchange.Client.Network;
using TelephoneExchange.Client.Models;

namespace TelephoneExchange.Client;

/// <summary>
/// Главное окно клиента
/// </summary>
public partial class MainWindow : Window
{
    private PhoneClient _phoneClient;
    private string _currentState = "Ожидание";
    private string _subscriberB = "";

    public MainWindow()
    {
        InitializeComponent();
        _phoneClient = new PhoneClient();
        SubscribeToEvents();
    }

    /// <summary>
    /// Подписаться на события PhoneClient
    /// </summary>
    private void SubscribeToEvents()
    {
        _phoneClient.OnAssignedNumber += number =>
        {
            Dispatcher.Invoke(() =>
            {
                txtMyNumber.Text = number;
            });
        };

        _phoneClient.OnStateChanged += state =>
        {
            Dispatcher.Invoke(() =>
            {
                _currentState = state;
                txtMyState.Text = state;
                UpdateButtonStates();
            });
        };

        _phoneClient.OnIncomingCall += number =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Входящий вызов от {number}", "Входящий вызов", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _subscriberB = number;
            });
        };

        _phoneClient.OnCallConnected += () =>
        {
            Dispatcher.Invoke(() =>
            {
                txtChatHistory.Clear();
                txtChatInput.IsEnabled = true;
                btnSendMessage.IsEnabled = true;
            });
        };

        _phoneClient.OnCallEnded += () =>
        {
            Dispatcher.Invoke(() =>
            {
                txtChatHistory.Clear();
                txtChatInput.IsEnabled = false;
                btnSendMessage.IsEnabled = false;
                txtChatInput.Clear();
            });
        };

        _phoneClient.OnMessageReceived += message =>
        {
            Dispatcher.Invoke(() =>
            {
                txtChatHistory.AppendText($"Абонент {_subscriberB}: {message}\n");
                txtChatHistory.ScrollToEnd();
            });
        };

        _phoneClient.OnSubscribersListReceived += subscribers =>
        {
            Dispatcher.Invoke(() =>
            {
                lstSubscribers.Items.Clear();
                foreach (var sub in subscribers)
                {
                    lstSubscribers.Items.Add(sub.ToString());
                }
            });
        };

        _phoneClient.OnDisconnected += () =>
        {
            Dispatcher.Invoke(() =>
            {
                txtConnectionStatus.Text = "отключен";
                txtConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
                btnConnect.Content = "Подключиться";
                txtServerAddress.IsEnabled = true;
                lstSubscribers.Items.Clear();
                txtMyNumber.Text = "-";
                txtMyState.Text = "-";
                UpdateButtonStates();
            });
        };

        _phoneClient.OnError += error =>
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        };
    }

    /// <summary>
    /// Обновить состояние кнопок в зависимости от текущего состояния
    /// </summary>
    private void UpdateButtonStates()
    {
        bool isConnected = _phoneClient.IsConnected;
        
        btnPickup.IsEnabled = isConnected && (_currentState == "Ожидание" || _currentState == "Входящий вызов");
        btnHangup.IsEnabled = isConnected && (_currentState == "Готов" || _currentState == "Входящий вызов" || 
                                               _currentState == "В разговоре" || _currentState == "Набор номера" || 
                                               _currentState == "Занято");
        btnDial.IsEnabled = isConnected && _currentState == "Готов";
    }

    private async void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        if (!_phoneClient.IsConnected)
        {
            // Подключение
            var parts = txtServerAddress.Text.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
            {
                MessageBox.Show("Неверный формат адреса. Используйте формат IP:Port", "Ошибка", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            btnConnect.IsEnabled = false;
            bool connected = await _phoneClient.Connect(parts[0], port);
            
            if (connected)
            {
                txtConnectionStatus.Text = "подключен";
                txtConnectionStatus.Foreground = System.Windows.Media.Brushes.Green;
                btnConnect.Content = "Отключиться";
                txtServerAddress.IsEnabled = false;
            }
            
            btnConnect.IsEnabled = true;
            UpdateButtonStates();
        }
        else
        {
            // Отключение
            _phoneClient.Disconnect();
        }
    }

    private void BtnPickup_Click(object sender, RoutedEventArgs e)
    {
        _phoneClient.SendPickup();
    }

    private void BtnHangup_Click(object sender, RoutedEventArgs e)
    {
        _phoneClient.SendHangup();
    }

    private void BtnDial_Click(object sender, RoutedEventArgs e)
    {
        var number = txtDialNumber.Text.Trim();
        if (string.IsNullOrEmpty(number))
        {
            MessageBox.Show("Введите номер телефона", "Ошибка", 
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _phoneClient.SendDial(number);
        txtDialNumber.Clear();
    }

    private void BtnSendMessage_Click(object sender, RoutedEventArgs e)
    {
        var message = txtChatInput.Text.Trim();
        if (string.IsNullOrEmpty(message))
            return;

        _phoneClient.SendMessage(message);
        txtChatHistory.AppendText($"Я: {message}\n");
        txtChatHistory.ScrollToEnd();
        txtChatInput.Clear();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _phoneClient.Disconnect();
    }
}