using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using System.Management;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Security;
using System.Runtime.InteropServices;

namespace Joexec {
  /// <summary>
  /// Interaction logic for MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window {
    CancellationTokenSource cts;
    public MainWindow() {      
      InitializeComponent();
      finishedT.Visibility = System.Windows.Visibility.Hidden;
      clBtn.IsEnabled = false;
    }

    private void buttonEn(bool enab) {
      hostBox.Focusable = enab;
      userBox.Focusable = enab;
      passBox.Focusable = enab;
      lclHostBtn.IsEnabled = enab;
      ldHostBtn.IsEnabled = enab;
      procBtn.IsEnabled = enab;
      CommandBox.IsEnabled = enab;
      clBtn.IsEnabled = !enab;
    }

    private void procBtn_Click(object sender, RoutedEventArgs e) {
      cts = new CancellationTokenSource();
      pBar.Value = 0;
      finishedT.Content = "Finished";
      finishedT.Visibility = System.Windows.Visibility.Hidden;
      string user = userBox.Text;
      SecureString notApassword = passBox.SecurePassword;
      string cmd = CommandBox.Text;
      string[] hosts = hostBox.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
      buttonEn(false);
      statusBox.Clear();
      statusBox.AppendText(DateTime.Now + System.Environment.NewLine);

      Task.Factory.StartNew(() => {
        foreach (string host in hosts) {
          joEx(host, user, notApassword, cmd, cts.Token);
          if (cts.IsCancellationRequested)
            break;
        }        
      });    
          
    }

    private void pBarUpdate() {
      int hosts = countHosts();
      double val = 100.0 / hosts;
      pBar.Value += val;
      if (pBar.Value >= 99.9) {
        finishedT.Visibility = System.Windows.Visibility.Visible;
        buttonEn(true);
      }
    }

    private int countHosts() {
      string[] hosts = hostBox.Text.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
      return hosts.Length;
    }

    private static string[] getHosts(string location) {
      string line;
      List<string> addresses = new List<string>();
      List<string> errors = new List<string>();
      using (StreamReader reader = new StreamReader(location)) {
        while ((line = reader.ReadLine()) != null) {
          try {
            if (line != "") {
              if (line.Substring(0, 1) != "#") {
                addresses.Add(line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[1]);
              }
            }
          } catch {
            errors.Add("#PF# "+line);
          }
        }
      }
      addresses.AddRange(errors);
      return addresses.ToArray();
    }

    private void joEx(string remoteMachine, string uname, SecureString notApassword, string cmd, CancellationToken ct) {
      string reply = "";
      try {
        var connection = new ConnectionOptions();
        connection.Username = uname;
        connection.Password = Marshal.PtrToStringUni(Marshal.SecureStringToGlobalAllocUnicode(notApassword));
        var wmiScope = new ManagementScope(String.Format("\\\\{0}\\root\\cimv2", remoteMachine), connection);
        var wmiProcess = new ManagementClass(wmiScope, new ManagementPath("Win32_Process"), new ObjectGetOptions());          
        var processes = new[] { cmd };
        wmiProcess.InvokeMethod("Create", processes);
        reply = "Sent success: " + remoteMachine + System.Environment.NewLine;
      } catch (Exception ex) {
        string err = ex.Message.ToString();
        if (err.Substring(4, 1) == "R") {
          err = "Unreachable.";
        }
        reply = remoteMachine + " error: " + err + System.Environment.NewLine;
      }
      if (!ct.IsCancellationRequested) {      
      pBar.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new System.Windows.Threading.DispatcherOperationCallback(delegate {
        pBarUpdate();
        statusBox.AppendText(reply);
        return null;
      }), null);
      }
    }

    private void lclHostBtn_Click(object sender, RoutedEventArgs e) {
      string[] hosts = getHosts("C:\\Windows\\System32\\drivers\\etc\\hosts");
      postHosts(hosts);
    }

    private void postHosts(string[] hosts) {
      hostBox.Clear();
      foreach (string host in hosts) {
        hostBox.AppendText(host + "\n");
      }
    }

    private void ldHostBtn_Click(object sender, RoutedEventArgs e) {
      Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
      Nullable<bool> result = dlg.ShowDialog();
      if (result == true) {
        string[] hosts = getHosts(dlg.FileName);
        postHosts(hosts);
      }
    }

    private void clBtn_Click(object sender, RoutedEventArgs e) {
      if (cts != null) {
        cts.Cancel();
        buttonEn(true);
        statusBox.AppendText("\nCancelled.\n");
        finishedT.Content = "Halted!";
        finishedT.Visibility = System.Windows.Visibility.Visible;
      }
    }
  }
}
