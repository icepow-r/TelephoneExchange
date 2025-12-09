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
    private string _currentState = "Idle";

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
                txtChatHistory.AppendText($"Собеседник: {message}\n");
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
                txtConnectionStatus.Text = "Отключен";
                txtConnectionStatus.Foreground = System.Windows.Media.Brushes.Red;
                btnConnect.Content = "Подключиться";
                txtServerAddress.IsEnabled = true;
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
        
        btnPickup.IsEnabled = isConnected && (_currentState == "Idle" || _currentState == "Ringing");
        btnHangup.IsEnabled = isConnected && (_currentState == "Ready" || _currentState == "Ringing" || 
                                               _currentState == "InCall" || _currentState == "Dialing" || 
                                               _currentState == "Busy");
        btnDial.IsEnabled = isConnected && _currentState == "Ready";
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
                txtConnectionStatus.Text = "Подключен";
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