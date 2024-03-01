using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;
using System.Threading;
using EncDec;
using System.Media;


namespace UdpChat
{
    public partial class ChatForm : Form
    {
        bool alive = false;
        UdpClient client;
        string userName;
        string lastSendedMessage;
        public ChatForm()
        {
            InitializeComponent();
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            Random rnd = new Random();
            userNameTextBox.Text = "User" + Convert.ToString(rnd.Next(0, 9999));
            KeyPreview = true;
            Desc.Text = "Для успешного соединения у всех участников должны совпадать настройки шифрования и адрес/порт рассылки.\r\n\r\nВ поле 'Адрес рассылки и порт' можно ввести любой адрес категории D для частных мультикаст-доменов (239.0.0.0-239.255.255.255). Эти адреса используются для многоадресной рассылки. Если системный администратор заблокировал порт по умолчанию или пакеты по каким-то причинам теряются, все участники должны использовать другой свободный порт и/или адрес.\r\n\r\n'Тип подключения Wi-Fi/другое' следует использовать только если часть сообщений теряется. Это особенность работы широковещательных рассылок, а не ошибка программы.\r\n\r\nСодержимое сообщений может быть расшифровано получателем только если его настройки безопасности совпадают с настройками отправителя, в противном случае программа отобразит сообщение об ошибке. При помощи режима комнат можно игнорировать такие сообщения и создавать множество изолированных друг от друга комнат с разными настройками безопасности. Либо можно использовать другой порт или адрес, тогда сообщения об ошибках отображаться не будут.";
            HelloText.Text = "Добро пожаловать в eNcChat.\r\n\r\nЭто децентрализованное средство обмена зашифрованными текстовыми сообщениями. Это значит, что для работы eNcChat не нужен сервер, а сообщения хранятся только в ОЗУ и только во время работы программы, а так же шифруются для безопасной передачи. \r\n\r\nВведите имя и нажмите 'Вход', чтобы войти со стандартными настройками и паролем. Учтите, что при входе со стандартным паролем Ваши сообщения будут видны всем остальным пользователям, но по-прежнему не могут быть расшифрованы посторонними.";
            chatTextBox.Text = "";
            loginButton.Enabled = true;
            logoutButton.Enabled = false;
            sendButton.Enabled = false;
            chatTextBox.ReadOnly = true;


            if (File.Exists("Settings.NConf"))
            {
                try
                {
                    DialogResult result = MessageBox.Show("Обнаружен существующий профиль настроек. Желаете загрузить его?", "Обнаружен профиль", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                    if (result == DialogResult.No)
                    {
                        return;
                    }
                    else
                    {
                        DecryptSettings();
                        ApplySettings();
                    }
                }
                catch
                { MessageBox.Show("Ошибка чтения профиля.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }

        }
        private void LoginButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (WiFi.Checked)
                    myTimer.Interval = 3000;
                else
                    myTimer.Interval = 30000;
                myTimer.Enabled = true;
                if (userNameTextBox.Text.Length == 0)
                { }
                else
                {
                    Chat.Visible = true;
                    ConnectButton.Enabled = false;
                    authorize.Visible = false;
                    userName = userNameTextBox.Text;
                    userNameTextBox.ReadOnly = true;
                    messageTextBox.Enabled = true;
                    passwordBox.ReadOnly = true;
                    chatTextBox.Text = "";
                    messageTextBox.Select();
                    try
                    {
                        logoutButton.Enabled = true;
                        client = new UdpClient(Convert.ToInt32(PortNumer.Value));
                        client.JoinMulticastGroup(IPAddress.Parse(ipBox.Text), 20);
                        Task receiveTask = new Task(ReceiveMessages);
                        receiveTask.Start();
                        chatTextBox.Text = "⯁Добро пожаловать, " + userName + ". Адрес: " + ipBox.Text + ":" + PortNumer.Value + ". Время: " + DateTime.Now.ToString("F") + ".\r\n⯁В поле 'ПУЛЬС' отображается время последнего обмена ЭХО-пакетами (необходимо для поддержания работы сети).\r\n\r\n";
                        if (DONTENCRYPT.Checked == true)
                        {
                            chatTextBox.Text = chatTextBox.Text + "\r\n\r\n\r\n\r\n========================================\r\nВНИМАНИЕ! ШИФРОВАНИЕ СООБЩЕНИЙ ОТКЛЮЧЕНО НАСТРОЙКАМИ!\r\n\r\n\r\n\r\nНИ В КОЕМ СЛУЧАЕ НЕ ПЕРЕДАВАЙТЕ КОНФИДЕНЦИАЛЬНЫЕ ДАННЫЕ!\r\n========================================\r\n\r\n\r\n\r\n";
                        }
                        string message = userName + " присоединился.";
                        if (DONTENCRYPT.Checked != true)
                        {
                            message = EncryptDecrypt.Encrypt(message, passwordBox.Text, Salt.Text, Convert.ToInt32(Iter.Value), Vector.Text);
                        }
                        byte[] data = Encoding.Unicode.GetBytes(message);
                        client.Send(data, data.Length, ipBox.Text, Convert.ToInt32(PortNumer.Value));
                        loginButton.Enabled = false;
                        sendButton.Enabled = true;
                    }
                    catch
                    {
                        MessageBox.Show("Ошибка отправки сообщения.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch
            { MessageBox.Show("Ошибка отправки сообщения.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void ReceiveMessages()
        {
            Pulse.BackColor = Color.LightGreen;
            alive = true;
            try
            {
                while (alive)
                {
                    bool showbadmessage = true;
                    bool techmessage = false;
                    IPEndPoint remoteIp = null;
                    byte[] data = client.Receive(ref remoteIp);
                    string message = Encoding.Unicode.GetString(data);
                    try
                    {
                        if (DONTENCRYPT.Checked != true)
                        {
                            message = EncryptDecrypt.Decrypt(message, passwordBox.Text, Salt.Text, Convert.ToInt32(Iter.Value), Vector.Text);
                        }

                        if (message == (DateTime.Now.ToString("dd")))
                        {
                            techmessage = true;
                        }
                    }
                    catch
                    {
                        if (ErrorCheck.Checked == false)
                        {
                            if (EncCheck.Checked == true && techmessage == false)
                                message = "❌ Расшифровка сообщения невозможна, настройки шифрования не совпадают ❌";
                            else { }
                        }
                        else showbadmessage = false;
                    }
                    {
                        Invoke(new MethodInvoker(() =>
                            {
                                if (techmessage == false)
                                {
                                    if (showbadmessage == true)
                                    {
                                        string time = DateTime.Now.ToShortTimeString();
                                        chatTextBox.Text = chatTextBox.Text + time + " " + message + "\r\n";
                                        if (ShowNotify.Checked == true)
                                        {
                                            if (message == lastSendedMessage)
                                            { }
                                            else
                                            {
                                                NotifyIcon NI = new NotifyIcon();
                                                NI.BalloonTipText = message;
                                                NI.BalloonTipTitle = "Новое сообщение!";
                                                NI.BalloonTipIcon = ToolTipIcon.Info;
                                                NI.Icon = this.Icon;
                                                NI.Visible = true;
                                                NI.ShowBalloonTip(300);
                                                NI.Dispose();
                                                Stream str = Res.Resources.sound;
                                                SoundPlayer snd = new SoundPlayer(str);
                                                snd.Play();
                                                snd.Dispose();
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    chatTextBox.Text = chatTextBox.Text;
                                    Pulse.Text = DateTime.Now.ToLongTimeString();
                                    Pulse.BackColor = Color.LightGreen;
                                }
                            }));
                    }

                }
            }
            catch (ObjectDisposedException)
            {
                if (!alive)
                    return;
                throw;
            }
            catch
            {
                MessageBox.Show("Ошибка приёма сообщения.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }
        private void SendButton_Click(object sender, EventArgs e)
        {
            if (messageTextBox.Text.Length == 0)
            { }
            else
            {
                try
                {
                    string message = String.Format(userName + ": " + messageTextBox.Text);
                    lastSendedMessage = message;
                    if (DONTENCRYPT.Checked != true)
                    {
                        message = EncryptDecrypt.Encrypt(message, passwordBox.Text, Salt.Text, Convert.ToInt32(Iter.Value), Vector.Text);
                    }
                    byte[] data = Encoding.Unicode.GetBytes(message);
                    client.Send(data, data.Length, ipBox.Text, Convert.ToInt32(PortNumer.Value));
                    messageTextBox.Clear();
                }
                catch
                {
                    MessageBox.Show("Ошибка отправки сообщения.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private void LogoutButton_Click(object sender, EventArgs e)
        {
            ConnectButton.Enabled = true;
            authorize.Visible = true;
            myTimer.Enabled = false;
            label1.Text = "Имя";
            ExitChat();
        }
        private void ExitChat()
        {
            try
            {
                string message = userName + " отключился.";
                if (DONTENCRYPT.Checked != true)
                {
                    message = EncryptDecrypt.Encrypt(message, passwordBox.Text, Salt.Text, Convert.ToInt32(Iter.Value), Vector.Text);
                }
                byte[] data = Encoding.Unicode.GetBytes(message);
                client.Send(data, data.Length, ipBox.Text, Convert.ToInt32(PortNumer.Value));
                alive = false;
                client.Close();
                loginButton.Enabled = true;
                logoutButton.Enabled = false;
                sendButton.Enabled = false;
                messageTextBox.Enabled = false;
                userNameTextBox.ReadOnly = false;
                passwordBox.ReadOnly = false;
                messageTextBox.Clear();
                chatTextBox.Clear();
                Chat.Visible = false;
            }
            catch { MessageBox.Show("Ошибка выхода.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (alive)
                ExitChat();
        }
        private void MessageTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                SendButton_Click(sender, e);
        }
        private void Button1_Click(object sender, EventArgs e)
        {
            chatTextBox.Clear();
        }
        private void ConnectButton_Click(object sender, EventArgs e)
        {
            Settings.Visible = true;
        }

        private void ApplySettings()
        {
            try
            {
                if (Salt.Text.Length == 0 || passwordBox.Text.Length == 0 || passwordBox.Text.Length == 0 || ipBox.Text.Length == 0)
                { MessageBox.Show("Заполнены не все поля!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
                else
                    if (Poverh.Checked == true)
                    TopMost = true;
                else TopMost = false;

                if (DarkMode.Checked == true)
                {
                    DeleteProfile.ForeColor = Color.White;
                    DeleteProfile.BackColor = Color.FromArgb(64, 64, 64);
                    ResetSettings.ForeColor = Color.White;
                    ResetSettings.BackColor = Color.FromArgb(64, 64, 64);
                    GenerateCrypt.ForeColor = Color.White;
                    GenerateCrypt.BackColor = Color.FromArgb(64, 64, 64);
                    ChatExport.ForeColor = Color.White;
                    ChatExport.BackColor = Color.FromArgb(64, 64, 64);
                    SaveToFile.ForeColor = Color.White;
                    SaveToFile.BackColor = Color.FromArgb(64, 64, 64);
                    LoadFromFile.ForeColor = Color.White;
                    LoadFromFile.BackColor = Color.FromArgb(64, 64, 64);
                    this.BackColor = Color.FromArgb(64, 64, 64);
                    HelloText.BackColor = Color.FromArgb(64, 64, 64);
                    HelloText.ForeColor = Color.White;
                    authorize.ForeColor = Color.White;
                    loginButton.ForeColor = Color.White;
                    loginButton.BackColor = Color.FromArgb(64, 64, 64);
                    ConnectButton.BackColor = Color.FromArgb(64, 64, 64);
                    ConnectButton.ForeColor = Color.White;
                    ApplyButton.BackColor = Color.FromArgb(64, 64, 64);
                    ApplyButton.ForeColor = Color.White;
                    AuthorButton.BackColor = Color.FromArgb(64, 64, 64);
                    AuthorButton.ForeColor = Color.White;
                    Desc.BackColor = Color.FromArgb(64, 64, 64);
                    Desc.ForeColor = Color.White;
                    Settings.ForeColor = Color.White;
                    groupBox2.ForeColor = Color.White;
                    SettingsBox.ForeColor = Color.White;
                    Settings.BackColor = Color.FromArgb(64, 64, 64);
                    ViewBox.ForeColor = Color.White;
                    EncryptBox.ForeColor = Color.White;
                    CleanChat.BackColor = Color.FromArgb(64, 64, 64);
                    CleanChat.ForeColor = Color.White;
                    chatTextBox.BackColor = Color.FromArgb(64, 64, 64);
                    chatTextBox.ForeColor = Color.White;
                    messageTextBox.BackColor = Color.FromArgb(64, 64, 64);
                    messageTextBox.ForeColor = Color.White;
                    logoutButton.BackColor = Color.FromArgb(64, 64, 64);
                    logoutButton.ForeColor = Color.White;
                    CleanChat.BackColor = Color.FromArgb(64, 64, 64);
                    CleanChat.ForeColor = Color.White;
                    sendButton.BackColor = Color.FromArgb(64, 64, 64);
                    sendButton.ForeColor = Color.White;
                    userNameTextBox.BackColor = Color.FromArgb(64, 64, 64);
                    userNameTextBox.ForeColor = Color.White;
                }
                else
                {
                    ChatExport.ForeColor = Color.Black;
                    ChatExport.BackColor = Color.White;
                    ResetSettings.ForeColor = Color.Black;
                    ResetSettings.BackColor = Color.White;
                    GenerateCrypt.ForeColor = Color.Black;
                    GenerateCrypt.BackColor = Color.White;
                    SaveToFile.ForeColor = Color.Black;
                    SaveToFile.BackColor = Color.White;
                    DeleteProfile.ForeColor = Color.Black;
                    DeleteProfile.BackColor = Color.White;
                    LoadFromFile.ForeColor = Color.Black;
                    LoadFromFile.BackColor = Color.White;
                    this.BackColor = Color.White;
                    HelloText.BackColor = Color.White;
                    HelloText.ForeColor = Color.Black;
                    authorize.ForeColor = Color.Black;
                    loginButton.ForeColor = Color.Black;
                    loginButton.BackColor = Color.White;
                    ConnectButton.BackColor = Color.White;
                    ConnectButton.ForeColor = Color.Black;
                    ApplyButton.BackColor = Color.White;
                    ApplyButton.ForeColor = Color.Black;
                    AuthorButton.BackColor = Color.White;
                    AuthorButton.ForeColor = Color.Black;
                    Desc.BackColor = Color.White;
                    Desc.ForeColor = Color.Black;
                    Settings.ForeColor = Color.Black;
                    groupBox2.ForeColor = Color.Black;
                    SettingsBox.ForeColor = Color.Black;
                    Settings.BackColor = Color.White;
                    ViewBox.ForeColor = Color.Black;
                    EncryptBox.ForeColor = Color.Black;
                    chatTextBox.BackColor = Color.White;
                    chatTextBox.ForeColor = Color.Black;
                    messageTextBox.BackColor = Color.White;
                    messageTextBox.ForeColor = Color.Black;
                    logoutButton.BackColor = Color.White;
                    logoutButton.ForeColor = Color.Black;
                    CleanChat.BackColor = Color.White;
                    CleanChat.ForeColor = Color.Black;
                    sendButton.BackColor = Color.White;
                    sendButton.ForeColor = Color.Black;
                    userNameTextBox.BackColor = Color.White;
                    userNameTextBox.ForeColor = Color.Black;
                    CleanChat.BackColor = Color.White;
                    CleanChat.ForeColor = Color.Black;
                }
                chatTextBox.Font = new Font("Microsoft Sans Serif", Convert.ToInt32(TextSize.Value));
                messageTextBox.Font = new Font("Microsoft Sans Serif", Convert.ToInt32(TextSize.Value));
                if (Vector.Text.Length / 8 == 0)
                {
                    MessageBox.Show("Длина вектора должна быть кратна 8! Невалидный вектор будет заменён на стандартный.", "Невалидный вектор", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Vector.Text = "q#@JJ^GxwQp9d?JL";
                }
            }
            catch
            { MessageBox.Show("Ошибка сохранения настроек.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
        private void ApplyButton_Click(object sender, EventArgs e)
        {
            ApplySettings();
            Settings.Visible = false;
        }
        private void UserNameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
            if (e.KeyCode == Keys.Enter)
                LoginButton_Click(sender, e);
        }
        private void ChatTextBox_TextChanged(object sender, EventArgs e)
        {
            chatTextBox.SelectionStart = chatTextBox.Text.Length;
            chatTextBox.ScrollToCaret();
        }
        private void MyTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                string message = (DateTime.Now.ToString("dd"));
                if (DONTENCRYPT.Checked != true)
                {
                    message = EncryptDecrypt.Encrypt(message, passwordBox.Text, Salt.Text, Convert.ToInt32(Iter.Value), Vector.Text);
                }
                byte[] data = Encoding.Unicode.GetBytes(message);
                client.Send(data, data.Length, ipBox.Text, Convert.ToInt32(PortNumer.Value));
            }
            catch
            { }
        }

        private void AuthorButton_Click(object sender, EventArgs e)
        {
            MessageBox.Show("eNcChat v.3.2\n\nby Naulex, 2023.", "Об авторе", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SaveToFile_Click(object sender, EventArgs e)
        {
            try
            {
                StreamWriter writefl;
                writefl = File.CreateText("Settings.NConf");
                writefl.Write(CreateSettingsString());
                writefl.Close();
                MessageBox.Show("Профиль успешно сохранён в файл \"Settings.NConf\".", "Профиль сохранён", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("Произошла ошибка сохранения файла. Убедитесь, что директория существует, и программе предоставлены все разрешения для чтения и записи.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private string CreateSettingsString()
        {
            string pass;
            pass = Microsoft.VisualBasic.Interaction.InputBox("Введите пароль для шифрования профиля.", "Шифрование профиля");
            if (pass == "")
            {
                MessageBox.Show("В целях безопасности запрещено сохранять данные в незашифрованном виде. Данные будут зашифрованы стандартным паролем \"eNcChat12345\"", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                pass = "eNcChat12345";
            }
            string FullHashString = "=====eNcChatSettings=====/eNcChatSettings/";
            FullHashString = FullHashString + passwordBox.Text;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + Vector.Text;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + Salt.Text;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + Iter.Value;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + ipBox.Text;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + PortNumer.Text;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + Ethernet.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + WiFi.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/";
            DialogResult result = MessageBox.Show("Желаете сохранить имя в профиль? Если профиль создаётся для передачи ключей другому участнику, имя сохранять не стоит.", "Сохранение имени", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                if (userNameTextBox.Text == "")
                { FullHashString = FullHashString + "User"; }
                else
                {
                    FullHashString = FullHashString + userNameTextBox.Text;
                }
            }
            if (result == DialogResult.No)
            { FullHashString = FullHashString + "User"; }
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + EncCheck.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + ErrorCheck.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + Poverh.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + DarkMode.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + TextSize.Value;
            FullHashString = FullHashString + "/eNcChatSettings/";
            FullHashString = FullHashString + ShowNotify.Checked;
            FullHashString = FullHashString + "/eNcChatSettings/=====eNcChatSettings=====";
            return EncryptDecrypt.Encrypt(FullHashString, pass, "yk*D4}3z#OuBf0G3", 512, "1QC83OYxf%~iOrcd");
        }

        private void DecryptSettings()
        {
            try
            {
                string pass, EncString;
                pass = Microsoft.VisualBasic.Interaction.InputBox("Введите пароль для расшифровки профиля.", "Расшифровка профиля");
                string KeyFile = File.ReadAllText("Settings.NConf");
                EncString = EncryptDecrypt.Decrypt(KeyFile, pass, "yk*D4}3z#OuBf0G3", 512, "1QC83OYxf%~iOrcd");
                pass = "";
                String[] Settings = EncString.Split(new string[] { "/eNcChatSettings/" }, StringSplitOptions.RemoveEmptyEntries);
                if (Settings[0] == "=====eNcChatSettings=====")
                {
                    passwordBox.Text = Settings[1];
                    Vector.Text = Settings[2];
                    Salt.Text = Settings[3];
                    Iter.Value = Convert.ToInt32(Settings[4]);
                    ipBox.Text = Settings[5];
                    PortNumer.Text = Settings[6];
                    if (Settings[7] == "True")
                        Ethernet.Checked = true;
                    else WiFi.Checked = true;
                    if (Settings[8] == "True")
                        WiFi.Checked = true;
                    else Ethernet.Checked = true;
                    userNameTextBox.Text = Settings[9];
                    if (Settings[10] == "True")
                        EncCheck.Checked = true;
                    else EncCheck.Checked = false;
                    if (Settings[11] == "True")
                        ErrorCheck.Checked = true;
                    else ErrorCheck.Checked = false;
                    if (Settings[12] == "True")
                        Poverh.Checked = true;
                    else Poverh.Checked = false;
                    if (Settings[13] == "True")
                        DarkMode.Checked = true;
                    else DarkMode.Checked = false;
                    TextSize.Value = Convert.ToInt32(Settings[14]);
                    if (Settings[15] == "True")
                        ShowNotify.Checked = true;
                    else ShowNotify.Checked = false;
                }
                else
                    MessageBox.Show("Данные имеют неверный формат.\n\nУбедитесь, что копируемый текст имеет строку\n\n=====eNcChatSettings=====\n\nв начале и в конце", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
            catch
            {
                MessageBox.Show("Ошибка расшифровки. Пароль указан правильно?", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            }
        }
        private void LoadFromFile_Click(object sender, EventArgs e)
        {
            DecryptSettings();
        }

        private void DeleteProfile_Click(object sender, EventArgs e)
        {
            if (File.Exists("Settings.NConf"))
            {
                try
                {
                    DialogResult result = MessageBox.Show("Вы действительно хотите удалить файл настроек?", "Удаление файла", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                    {
                        File.Delete("Settings.NConf");
                        MessageBox.Show("Файл удалён.", "Успех", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }

                }
                catch
                { MessageBox.Show("Ошибка удаления файла.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Information); }

            }
            else
            {
                MessageBox.Show("Файл не обнаружен!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ChatExport_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Фрагмент чата будет сохранён в незашифрованном виде. Кроме того, все участники чата будут проинформированы об этом сохранении. Желаете продолжить?", "Незашифрованное сохранение", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (result == DialogResult.No)
            {
                return;
            }
            else
            {
                try
                {
                    SaveFileDialog saveFileDialog = new SaveFileDialog();
                    saveFileDialog.Filter = "Текстовый документ (*.txt)|*.txt";
                    saveFileDialog.FileName = "SavedChat_" + DateTime.Now.ToShortDateString() + ".txt";

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        StreamWriter streamWriter = new StreamWriter(saveFileDialog.FileName);
                        streamWriter.WriteLine("==========СОХРАНЁННЫЙ ЧАТ==========");
                        streamWriter.WriteLine(chatTextBox.Text);
                        streamWriter.WriteLine("==========СОХРАНЁННЫЙ ЧАТ==========");
                        streamWriter.Close();
                        string message = "=====❗❗❗ " + userName + " СОХРАНИЛ ЧАТ В ФАЙЛ❗❗❗=====";
                        if (DONTENCRYPT.Checked != true)
                        {
                            message = EncryptDecrypt.Encrypt(message, passwordBox.Text, Salt.Text, Convert.ToInt32(Iter.Value), Vector.Text);
                        }
                        byte[] data = Encoding.Unicode.GetBytes(message);
                        client.Send(data, data.Length, ipBox.Text, Convert.ToInt32(PortNumer.Value));
                    }
                }
                catch
                { MessageBox.Show("Ошибка сохранения.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Information); }
            }
        }

        private void ShowKeys_CheckedChanged(object sender, EventArgs e)
        {
            if (ShowKeys.Checked == true)
            {
                passwordBox.UseSystemPasswordChar = false;
                Vector.UseSystemPasswordChar = false;
                Salt.UseSystemPasswordChar = false;
            }

            else
            {
                passwordBox.UseSystemPasswordChar = true;
                Vector.UseSystemPasswordChar = true;
                Salt.UseSystemPasswordChar = true;
            }
        }

        string GenerateRandomString()
        {
            Random rnd = new Random();
            string s0 = "";
            string s1 = "";
            int n;
            string st = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+";
            for (int j = 0; j < 16; j++)
            {
                n = rnd.Next(0, 73);
                s1 = st.Substring(n, 1);
                s0 += s1;
            }

            return s0;
        }

        private void GenerateCrypt_Click(object sender, EventArgs e)
        {

            passwordBox.Text = GenerateRandomString();
            Thread.Sleep(50);
            Vector.Text = GenerateRandomString();
            Thread.Sleep(50);
            Salt.Text = GenerateRandomString();
            Thread.Sleep(50);
            Random rnd = new Random();
            Iter.Value = rnd.Next(512, 5192);
        }

        private void ResetSettings_Click(object sender, EventArgs e)
        {
            passwordBox.Text = "Y&Xb%?)c%XQ>?veW";
            Vector.Text = "q#1JJ^GxwQp9d?JL";
            Salt.Text = "!<9Te,gVk'YH~g1n";
            Iter.Value = 512;
            ipBox.Text = "239.148.32.70";
            PortNumer.Value = 49153;
            Ethernet.Checked = true;
            EncCheck.Checked = true;
            ErrorCheck.Checked = false;
            Poverh.Checked = false;
            DarkMode.Checked = false;
            TextSize.Value = 9;
        }

        private void DONTENCRYPT_CheckedChanged(object sender, EventArgs e)
        {
            if (DONTENCRYPT.Checked == true)
            {
                string ans;
                ans = Microsoft.VisualBasic.Interaction.InputBox("Категорически не рекомендуется отключать шифрование сообщений!\nДействуйте на свой страх и риск.\nДля подтверждения наберите фразу \"ОТКЛЮЧИТЬ ШИФРОВАНИЕ\"", "ОТКЛЮЧЕНИЕ ШИФРОВАНИЯ");
                if
                    (ans != "ОТКЛЮЧИТЬ ШИФРОВАНИЕ")
                {
                    DONTENCRYPT.Checked = false;
                    DONTENCRYPT.BackColor = Color.White;
                    this.Text = "eNcChat";
                }
                else
                {
                    DONTENCRYPT.BackColor = Color.Red;
                    MessageBox.Show("ШИФРОВАНИЕ СООБЩЕНИЙ ОТКЛЮЧЕНО. \nРАЗРАБОТЧИК НЕ НЕСЁТ ОТВЕТСТВЕННОСТИ ЗА ВСЕ ВОЗМОЖНЫЕ УБЫТКИ, ПОВЛЕЧЁННЫЕ ОТКЛЮЧЕНИЕМ ШИФРОВАНИЯ!", "ВНИМАНИЕ", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Text = this.Text + " ===РЕЖИМ БЕЗ ШИФРОВАНИЯ===";
                }
            }
            if (DONTENCRYPT.Checked == false)
            {
                DONTENCRYPT.BackColor = Color.White;
                this.Text = "eNcChat";
            }
        }
    }
}