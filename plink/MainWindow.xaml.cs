using System.Windows.Controls;

namespace plink
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.NetworkInformation;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Security.Policy;
    using System.Security.Principal;
    using System.Text;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Navigation;
    using System.Xml.Linq;
    using Microsoft.Win32;
    using Wpf.Ui.Controls;

    public partial class MainWindow : FluentWindow
    {
        //Zmień tekst w cudzysłowie poniżej, aby ustawić domyślną nazwę pliku dziennika. Domyślną wartością jest MikrotikFirewallCommandLog.txt.
        private static readonly string logFilePath = "MikrotikFirewallCommandLog.txt";
        //Zmień liczbę poniżej, aby ustawić maksymalną liczbę adresów do przepuszczenia, jaką da się dodać do listy. Domyślną wartością jest 10.
        private static readonly uint maxAllowed = 10;
        public MainWindow()
        {
            InitializeComponent();
            //Na wzór niżej zakomentowanych linii możesz dodawać adresy, które mają znaleźć się na liście w momencie uruchomienia programu. Aby odkomentować, usuń "//". Domyślnie nic nie jest dodawane.
            //UrlListBox.Items.Add("enaw.smarthost.pl");
            //UrlListBox.Items.Add("onet.pl");
        }

        private async void SetFirewallRule(object sender, RoutedEventArgs e)
        {
            ApplyButton.IsEnabled = false;
            ClearButton.IsEnabled = false;
            if(UrlListBox.Items.Count == 0)
            {
                System.Windows.MessageBox.Show("Nie podano żadnego adresu do przepuszczenia.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                ApplyButton.IsEnabled = true;
                ClearButton.IsEnabled = true;
                return;
            }
            string driveLetter = (System.IO.Path.GetPathRoot(AppContext.BaseDirectory) ?? "")[0].ToString();
            string plinkPath = $"{driveLetter}:\\plink.exe";
            string login = LoginTextBox.Text;
            string password = PasswordBox.Password;
            (string localIp, string gatewayIp) = GetLocalIPAndGateway();
            if (string.IsNullOrEmpty(localIp))
            {
                System.Windows.MessageBox.Show("Program nie może kontynuować działania z powodu braku adresu IP. Sprawdź swoje połączenie z siecią i spróbuj ponownie.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                FailedResultTextBlock.Text = "❌ Wystąpił błąd podczas dodawania reguły.";
                SuccessResultTextBlock.Visibility = Visibility.Collapsed;
                ProgressResultGrid.Visibility = Visibility.Collapsed;
                FailedResultTextBlock.Visibility = Visibility.Visible;
                string logEntry = $"Operation failed ({DateTime.Now}) - IP address not found.\n\n";
                File.AppendAllText(logFilePath, logEntry);
                ApplyButton.IsEnabled = true;
                ClearButton.IsEnabled = true;
                return;
            }
            string comment = "VistaaaMikrotikFirewall";
            string networkIp = GetNetworkAddress(localIp);
            string dnsIp = GetComputerDnsIp();
            string command = $"@echo off\n"
                + $"\"{plinkPath}\" -ssh {login}@{gatewayIp} -pw {password} "
                + $"/ip firewall filter add chain=forward src-address=!{localIp} out-interface=vlan222 action=drop comment=\"{comment}\" place-before=*0; "
                + $"\"/ip firewall filter add action=accept chain=forward disabled=no dst-address={dnsIp} out-interface=vlan222 src-address={networkIp} comment=\"{comment}\" place-before=*0; ";
            foreach (string item in UrlListBox.Items)
                command += $"/ip firewall address-list add list={comment} address={item} comment=\"{comment}\"; ";
            command += 
                $"/ip firewall filter add chain=forward src-address={networkIp} dst-address-list={comment} action=accept comment=\"{comment}\" place-before=*0; \n"
                + "pause";
            if (!File.Exists(plinkPath))
            {
                string plinkURL = "https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe";
                if (!await DownloadPlinkAsync(plinkURL, plinkPath))
                {
                    System.Windows.MessageBox.Show($"Nie udało się pobrać pliku plink.exe. Sprawdź swoje połączenie z internetem i/lub uprawnienia, a następnie spróbuj ponownie.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    FailedResultTextBlock.Text = "❌ Wystąpił błąd podczas dodawania reguły.";
                    SuccessResultTextBlock.Visibility = Visibility.Collapsed;
                    ProgressResultGrid.Visibility = Visibility.Collapsed;
                    FailedResultTextBlock.Visibility = Visibility.Visible;
                    string logEntry = $"Operation failed ({DateTime.Now}) - Downloading plink.exe failed:\n{command}\n\n";
                    File.AppendAllText(logFilePath, logEntry);
                    ApplyButton.IsEnabled = true;
                    ClearButton.IsEnabled = true;
                    return;
                }
            }
            ProgressResultTextBlock.Text = "⌛ Dodawanie reguły...";
            ProgressResultGrid.Visibility = Visibility.Visible;
            SuccessResultTextBlock.Visibility = Visibility.Collapsed;
            FailedResultTextBlock.Visibility = Visibility.Collapsed;
            bool result = await RunCommand(command);
            if (result)
            {
                SuccessResultTextBlock.Text = "✔ Pomyślnie dodano regułę.";
                FailedResultTextBlock.Visibility = Visibility.Collapsed;
                ProgressResultGrid.Visibility = Visibility.Collapsed;
                SuccessResultTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                FailedResultTextBlock.Text = "❌ Wystąpił błąd podczas dodawania reguły.";
                SuccessResultTextBlock.Visibility = Visibility.Collapsed;
                ProgressResultGrid.Visibility = Visibility.Collapsed;
                FailedResultTextBlock.Visibility = Visibility.Visible;
            }
            ApplyButton.IsEnabled = true;
            ClearButton.IsEnabled = true;
        }

        private async void ClearFirewallRule(object sender, RoutedEventArgs e)
        {
            ClearButton.IsEnabled = false;
            ApplyButton.IsEnabled = false;
            string driveLetter = (System.IO.Path.GetPathRoot(AppContext.BaseDirectory) ?? "")[0].ToString();
            string plinkPath = $"{driveLetter}:\\plink.exe";
            string login = LoginTextBox.Text;
            string password = PasswordBox.Password;
            (string _, string gatewayIp) = GetLocalIPAndGateway();
            string comment = "VistaaaMikrotikFirewall";
            string command = $"@echo off\n"
                + $"\"{plinkPath}\" -ssh {login}@{gatewayIp} -pw {password} "
                + $"\"/ip firewall filter remove [find comment=\"{comment}\"]; "
                + $"/ip firewall address-list remove [find comment=\"{comment}\"]\"\n"
                + "pause";
            if (!File.Exists(plinkPath))
            {
                string plinkURL = "https://the.earth.li/~sgtatham/putty/latest/w64/plink.exe";
                if (!await DownloadPlinkAsync(plinkURL, plinkPath))
                {
                    System.Windows.MessageBox.Show($"Nie udało się pobrać pliku plink.exe. Sprawdź swoje połączenie z internetem i/lub uprawnienia, a następnie spróbuj ponownie.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    FailedResultTextBlock.Text = "❌ Wystąpił błąd podczas usuwania reguły.";
                    SuccessResultTextBlock.Visibility = Visibility.Collapsed;
                    ProgressResultGrid.Visibility = Visibility.Collapsed;
                    FailedResultTextBlock.Visibility = Visibility.Visible;
                    string logEntry = $"Operation failed ({DateTime.Now}) - Downloading plink.exe failed:\n{command}\n\n";
                    File.AppendAllText(logFilePath, logEntry);
                    ClearButton.IsEnabled = true;
                    ApplyButton.IsEnabled = true;
                    return;
                }
            }
            ProgressResultTextBlock.Text = "⌛ Usuwanie reguły...";
            ProgressResultGrid.Visibility = Visibility.Visible;
            SuccessResultTextBlock.Visibility = Visibility.Collapsed;
            FailedResultTextBlock.Visibility = Visibility.Collapsed;
            bool result = await RunCommand(command);
            if (result)
            {
                SuccessResultTextBlock.Text = "✔ Pomyślnie usunięto regułę.";
                FailedResultTextBlock.Visibility = Visibility.Collapsed;
                ProgressResultGrid.Visibility = Visibility.Collapsed;
                SuccessResultTextBlock.Visibility = Visibility.Visible;
            }
            else
            {
                FailedResultTextBlock.Text = "❌ Wystąpił błąd podczas usuwania reguły.";
                SuccessResultTextBlock.Visibility = Visibility.Collapsed;
                ProgressResultGrid.Visibility = Visibility.Collapsed;
                FailedResultTextBlock.Visibility = Visibility.Visible;
            }
            ClearButton.IsEnabled = true;
            ApplyButton.IsEnabled = true;
        }

        private static async Task<bool> RunCommand(string command)
        {
            try
            {
                ProcessStartInfo startInfo = new()
                {
                    FileName = "cmd.exe",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (Process proccess = new() { StartInfo = startInfo })
                {
                    proccess.Start();
                    using (StreamWriter sw = proccess.StandardInput)
                    {
                        if (sw.BaseStream.CanWrite)
                        {
                            await sw.WriteLineAsync(command);
                            await sw.WriteLineAsync();
                        }
                    }
                    await proccess.WaitForExitAsync();
                }
                string logEntry = $"Operation completed successfully ({DateTime.Now}):\n{command}\n\n";
                File.AppendAllText(logFilePath, logEntry);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                string logEntry = $"Operation failed ({DateTime.Now}) - {ex.Message}:\n{command}\n\n";
                File.AppendAllText(logFilePath, logEntry);
                return false;
            }
        }

        private static (string localIp, string gatewayIp) GetLocalIPAndGateway()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up)
                {
                    var properties = networkInterface.GetIPProperties();
                    var ipv4Address = properties.UnicastAddresses
                        .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();
                    var gatewayAddress = properties.GatewayAddresses.FirstOrDefault()?.Address.ToString();

                    if (!string.IsNullOrEmpty(ipv4Address) && !string.IsNullOrEmpty(gatewayAddress))
                    {
                        return (ipv4Address, gatewayAddress);
                    }
                }
            }
            return ("", "");
        }

        private static string GetNetworkAddress(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return "";
            string[] ipParts = ipAddress.Split('.');
            ipParts[3] = "0";
            return string.Join(".", ipParts) + "/24";
        }

        private static string GetComputerDnsIp()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up && HasGateway(networkInterface))
                {
                    var dnsAddresses = networkInterface.GetIPProperties().DnsAddresses;
                    var dnsIp = dnsAddresses.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)?.ToString();
                    if (!string.IsNullOrEmpty(dnsIp))
                    {
                        return dnsIp;
                    }
                }
            }
            return "0.0.0.0";
        }

        private static bool HasGateway(NetworkInterface networkInterface)
        {
            return networkInterface.GetIPProperties().GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork);
        }

        private void ShowLogFile(object sender, RoutedEventArgs e)
        {
            try
            {
                if (File.Exists(logFilePath))
                {
                    Process.Start(new ProcessStartInfo(logFilePath) { UseShellExecute = true });
                }
                else
                {
                    System.Windows.MessageBox.Show("Plik dziennika nie istnieje. Dziennik wraz z zawartością pojawi się po pierwszej wykonanej operacji i będzie się wydłużał w miarę wykonywania kolejnych operacji.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private async Task<bool> DownloadPlinkAsync(string url, string destinationPath)
        {
            ProgressResultTextBlock.Text = "⌛ Pobieranie plink.exe...";
            ProgressResultGrid.Visibility = Visibility.Visible;
            SuccessResultTextBlock.Visibility = Visibility.Collapsed;
            FailedResultTextBlock.Visibility = Visibility.Collapsed;
            try
            {
                using HttpClient client = new();
                byte[] data = await client.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(destinationPath, data);
                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(ex.Message, "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (UrlListBox.Items.Count < maxAllowed)
            {
                if(string.IsNullOrEmpty(UrlTextBox.Text.Trim()))
                {
                    System.Windows.MessageBox.Show("Podaj adres, aby dodać go do listy.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                else if (UrlListBox.Items.Contains(UrlTextBox.Text.Trim()))
                    System.Windows.MessageBox.Show("Podany adres już istnieje na liście.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
                else
                {
                    UrlListBox.Items.Add(UrlTextBox.Text.Trim());
                    UrlTextBox.Text = "";
                }                  
            }            
            else
                System.Windows.MessageBox.Show($"Wykorzystano maksymalną liczbę adresów do przepuszczenia ({maxAllowed}). Aby dodać nowy, usuń dowolny istniejący. Możesz to zrobić korzystając z menu kontekstowego pod prawym przycikiem myszy.", "Błąd", System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void MenuItemRemove_Click(object sender, RoutedEventArgs e)
        {
            if(UrlListBox.Items.Count == 1)
                UrlListBox.Items.Clear();
            else if (UrlListBox.SelectedItem is not null)
            {
                UrlListBox.Items.Remove(UrlListBox.SelectedItem);
                UrlListBox.SelectedItem = null;
            }
                
        }

    }

}
