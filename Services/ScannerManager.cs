using System;
using System.IO;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace WatchCollection.Services;

/// <summary>
/// Gère la communication avec un scanner code-barre USB (M900D ou Arduino UNO)
/// connecté en port COM. Cross-platform : Windows et Linux.
/// </summary>
public sealed class ScannerManager : IDisposable
{
    private const int BaudRate = 9600;
    private const int ReadWriteTimeoutMs = 10000;

    private SerialPort? _serialPort;
    private bool _disposed;

    /// <summary>
    /// Événement déclenché lorsqu'un code-barre est lu.
    /// L'argument string contient le code-barre scanné (= Barcode de la montre).
    /// </summary>
    public event EventHandler<string>? BarcodeScanned;

    /// <summary>
    /// Indique si le scanner est connecté et prêt.
    /// </summary>
    public bool IsConnected => _serialPort?.IsOpen ?? false;

    /// <summary>
    /// Tente d'ouvrir une connexion avec le scanner.
    /// </summary>
    public bool TryOpenPort(out string? errorMessage)
    {
        errorMessage = null;
        try
        {
            CloseInternal();

            var portName = DetectScannerPort();
            if (portName is null)
            {
                errorMessage = "Aucun scanner détecté. Branchez le scanner USB et réessayez.";
                return false;
            }

            _serialPort = new SerialPort
            {
                BaudRate = BaudRate,
                PortName = portName,
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = ReadWriteTimeoutMs,
                WriteTimeout = ReadWriteTimeoutMs
            };

            _serialPort.DataReceived += OnDataReceived;
            _serialPort.Open();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            errorMessage = "Le port est déjà utilisé par une autre application.";
            CloseInternal();
            return false;
        }
        catch (IOException ex)
        {
            errorMessage = $"Erreur de communication avec le scanner : {ex.Message}";
            CloseInternal();
            return false;
        }
        catch (Exception ex)
        {
            errorMessage = $"Erreur inattendue : {ex.Message}";
            CloseInternal();
            return false;
        }
    }

    /// <summary>
    /// Ferme proprement la connexion avec le scanner.
    /// </summary>
    public void ClosePort() => CloseInternal();

    /// <summary>
    /// Détecte le port série du scanner. Cross-platform via SerialPort.GetPortNames()
    /// qui renvoie les COM* sous Windows et les /dev/tty* sous Linux.
    /// </summary>
    private static string? DetectScannerPort()
    {
        var ports = SerialPort.GetPortNames();

        if (ports.Length == 0)
            return null;

        // Sur Linux, on privilégie les ports USB-série typiques d'un scanner ou Arduino
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            foreach (var p in ports)
            {
                if (p.Contains("ttyUSB", StringComparison.OrdinalIgnoreCase)
                    || p.Contains("ttyACM", StringComparison.OrdinalIgnoreCase))
                    return p;
            }
        }

        // Par défaut : premier port disponible
        return ports[0];
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (sender is not SerialPort port || !port.IsOpen) return;

            var rawData = port.ReadExisting();
            var barcode = rawData.Trim('\r', '\n', ' ');

            if (!string.IsNullOrEmpty(barcode))
                BarcodeScanned?.Invoke(this, barcode);
        }
        catch (IOException)
        {
            // Erreur de lecture transitoire : on ignore, le scanner peut envoyer des trames partielles
        }
    }

    private void CloseInternal()
    {
        if (_serialPort is null) return;

        try
        {
            _serialPort.DataReceived -= OnDataReceived;
            if (_serialPort.IsOpen) _serialPort.Close();
            _serialPort.Dispose();
        }
        catch (IOException)
        {
            // Port déjà fermé ou inaccessible : on ignore proprement
        }
        finally
        {
            _serialPort = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        CloseInternal();
        _disposed = true;
    }
}