using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TelephoneExchange.Client.Models;

namespace TelephoneExchange.Client.Network
{
    /// <summary>
    /// Клиент для подключения к серверу АТС
    /// </summary>
    public class PhoneClient
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private bool _isRunning;

        public bool IsConnected => _tcpClient?.Connected ?? false;

        // События
        public event Action<string>? OnAssignedNumber;
        public event Action<string>? OnSignalReceived;
        public event Action<string>? OnStateChanged;
        public event Action<string>? OnIncomingCall;
        public event Action? OnCallConnected;
        public event Action? OnCallEnded;
        public event Action<string>? OnMessageReceived;
        public event Action<List<SubscriberInfo>>? OnSubscribersListReceived;
        public event Action? OnDisconnected;
        public event Action<string>? OnError;

        /// <summary>
        /// Подключиться к серверу
        /// </summary>
        public async Task<bool> Connect(string serverAddress, int port)
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(serverAddress, port);
                _networkStream = _tcpClient.GetStream();
                
                // Отправить команду подключения
                SendCommand("CONNECT");
                
                // Начать прослушивание
                StartListening();
                
                return true;
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка подключения: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Отключиться от сервера
        /// </summary>
        public void Disconnect()
        {
            _isRunning = false;
            
            try
            {
                _networkStream?.Close();
                _tcpClient?.Close();
            }
            catch { }
            
            OnDisconnected?.Invoke();
        }

        /// <summary>
        /// Отправить команду "снять трубку"
        /// </summary>
        public void SendPickup()
        {
            SendCommand("PICKUP");
        }

        /// <summary>
        /// Отправить команду "положить трубку"
        /// </summary>
        public void SendHangup()
        {
            SendCommand("HANGUP");
        }

        /// <summary>
        /// Отправить команду "набрать номер"
        /// </summary>
        public void SendDial(string number)
        {
            SendCommand($"DIAL:{number}");
        }

        /// <summary>
        /// Отправить сообщение
        /// </summary>
        public void SendMessage(string message)
        {
            SendCommand($"MESSAGE:{message}");
        }

        /// <summary>
        /// Отправить команду на сервер
        /// </summary>
        private void SendCommand(string command)
        {
            try
            {
                if (_networkStream != null && _tcpClient?.Connected == true)
                {
                    var data = Encoding.UTF8.GetBytes(command + "\n");
                    _networkStream.Write(data, 0, data.Length);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка отправки команды: {ex.Message}");
            }
        }

        /// <summary>
        /// Начать прослушивание сообщений от сервера
        /// </summary>
        private void StartListening()
        {
            _isRunning = true;
            
            Task.Run(async () =>
            {
                try
                {
                    var buffer = new byte[4096];
                    var messageBuilder = new StringBuilder();

                    while (_isRunning && _tcpClient?.Connected == true && _networkStream != null)
                    {
                        int bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0)
                            break;

                        var data = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        messageBuilder.Append(data);

                        // Обработка сообщений, разделенных переводом строки
                        var messages = messageBuilder.ToString().Split('\n');
                        for (int i = 0; i < messages.Length - 1; i++)
                        {
                            var message = messages[i].Trim();
                            if (!string.IsNullOrEmpty(message))
                            {
                                ProcessMessage(message);
                            }
                        }
                        messageBuilder.Clear();
                        messageBuilder.Append(messages[^1]);
                    }
                }
                catch (Exception ex)
                {
                    if (_isRunning)
                    {
                        OnError?.Invoke($"Ошибка чтения: {ex.Message}");
                    }
                }
                finally
                {
                    Disconnect();
                }
            });
        }

        /// <summary>
        /// Обработать сообщение от сервера
        /// </summary>
        private void ProcessMessage(string message)
        {
            try
            {
                if (message.StartsWith("ASSIGNED:"))
                {
                    var number = message.Substring(9);
                    OnAssignedNumber?.Invoke(number);
                }
                else if (message.StartsWith("SIGNAL:"))
                {
                    var signal = message.Substring(7);
                    OnSignalReceived?.Invoke(signal);
                }
                else if (message.StartsWith("STATE:"))
                {
                    var state = message.Substring(6);
                    OnStateChanged?.Invoke(state);
                }
                else if (message.StartsWith("INCOMING_CALL:"))
                {
                    var number = message.Substring(14);
                    OnIncomingCall?.Invoke(number);
                }
                else if (message == "CALL_CONNECTED")
                {
                    OnCallConnected?.Invoke();
                }
                else if (message == "CALL_ENDED")
                {
                    OnCallEnded?.Invoke();
                }
                else if (message.StartsWith("MESSAGE:"))
                {
                    var text = message.Substring(8);
                    OnMessageReceived?.Invoke(text);
                }
                else if (message.StartsWith("SUBSCRIBERS:"))
                {
                    var json = message.Substring(12);
                    var subscribers = JsonSerializer.Deserialize<List<SubscriberInfo>>(json);
                    if (subscribers != null)
                    {
                        OnSubscribersListReceived?.Invoke(subscribers);
                    }
                }
                else if (message.StartsWith("ERROR:"))
                {
                    var error = message.Substring(6);
                    OnError?.Invoke(error);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Ошибка обработки сообщения: {ex.Message}");
            }
        }
    }
}
