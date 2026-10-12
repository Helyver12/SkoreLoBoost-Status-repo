using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Deceive;

internal static class Utils
{
    internal static string DeceiveVersion
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null)
                return "v0.0.0";
            return "v" + version.Major + "." + version.Minor + "." + version.Build;
        }
    }

    /**
     * Asynchronously checks if the current version of Deceive is the latest version.
     * If not, and the user has not dismissed the message before, an alert is shown.
     */
    public static async Task CheckForUpdatesAsync()
    {
        try
        {
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkoreLoBoostStatus", DeceiveVersion));

            var response =
                await httpClient.GetAsync("https://api.github.com/repos/Helyver12/SkoreLoBoost-Status-repo/releases/latest");
            var content = await response.Content.ReadAsStringAsync();
            var release = JsonSerializer.Deserialize<JsonNode>(content);
            var latestVersion = release?["tag_name"]?.ToString();

            // If failed to fetch or already latest or newer, return.
            if (latestVersion is null)
                return;
            var githubVersion = new Version(latestVersion.Replace("v", ""));
            var assemblyVersion = new Version(DeceiveVersion.Replace("v", ""));
            // Earlier = -1, Same = 0, Later = 1
            if (assemblyVersion.CompareTo(githubVersion) != -1)
                return;

            // Check if we have shown this before.
            var latestShownVersion = Persistence.GetPromptedUpdateVersion();

            // If we have, return.
            if (!string.IsNullOrEmpty(latestShownVersion) && latestShownVersion == latestVersion)
                return;

            // Show a message and record the latest shown.
            Persistence.SetPromptedUpdateVersion(latestVersion);

            var result = MessageBox.Show(
                $"Há uma nova versão do SkoreLoBoost Status: {latestVersion}. Você está usando a {DeceiveVersion}. " +
                "As atualizações costumam corrigir erros importantes ou se adaptar a mudanças da Riot, então é recomendado instalar a versão mais recente.\n\n" +
                "Clique em OK para abrir a página de download ou em Cancelar para continuar. Se você cancelar, não vamos mostrar este aviso de novo para esta versão.",
                StartupHandler.DeceiveTitle,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button1
            );

            // Open the release page in the browser, but only if it really is this project's page.
            var releaseUrl = release?["html_url"]?.ToString();
            if (result is DialogResult.OK && releaseUrl is not null && releaseUrl.StartsWith("https://github.com/Helyver12/"))
                Process.Start(releaseUrl);
        }
        catch
        {
            // Ignored.
        }
    }

    private static IEnumerable<Process> GetProcesses()
    {
        var riotCandidates = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
            .Where(process => process.Id != Process.GetCurrentProcess().Id).ToList();
        riotCandidates.AddRange(Process.GetProcessesByName("LeagueClient"));
        riotCandidates.AddRange(Process.GetProcessesByName("LoR"));
        riotCandidates.AddRange(Process.GetProcessesByName("VALORANT-Win64-Shipping"));
        riotCandidates.AddRange(Process.GetProcessesByName("RiotClientServices"));
        return riotCandidates;
    }

    // Return the currently running Riot Client process, or null if none are running.
    public static Process? GetRiotClientProcess() => Process.GetProcessesByName("RiotClientServices").FirstOrDefault();

    // Checks if there is a running LCU/LoR/VALORANT/RC or Deceive instance.
    public static bool IsClientRunning() => GetProcesses().Any();

    // Kills the running LCU/LoR/VALORANT/RC or Deceive instance, if applicable.
    public static void KillProcesses()
    {
        try
        {
            foreach (var process in GetProcesses())
            {
                process.Refresh();
                if (process.HasExited)
                    continue;
                process.Kill();
                process.WaitForExit();
            }
        }
        catch (Win32Exception ex)
        {
            // thank you C# and your horrible win32 ecosystem integration, I have no clue if this is correct
            if (ex.NativeErrorCode == -2147467259 || ex.ErrorCode == -2147467259 || ex.ErrorCode == 5 ||
                ex.NativeErrorCode == 5)
            {
                // ERROR_ACCESS_DENIED
                MessageBox.Show(
                    "O SkoreLoBoost Status não conseguiu fechar os processos da Riot por falta de permissão. Abra o aplicativo como administrador e tente de novo.",
                    StartupHandler.DeceiveTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1
                );
                Environment.Exit(0);
            }

            throw ex;
        }
    }

    // Checks for any installed Riot Client configuration,
    // and returns the path of the client if it does. Else, returns null.
    public static string? GetRiotClientPath()
    {
        // Find the RiotClientInstalls file.
        var installPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Riot Games/RiotClientInstalls.json");
        if (!File.Exists(installPath))
            return null;

        try
        {
            // occasionally this deserialization may error, because the RC occasionally corrupts its own
            // configuration file (wtf riot?). we will return null in that case, which will cause a prompt
            // telling the user to launch a game normally once
            var data = JsonSerializer.Deserialize<JsonNode>(File.ReadAllText(installPath));
            var rcPaths = new List<string?>
                { data?["rc_default"]?.ToString(), data?["rc_live"]?.ToString(), data?["rc_beta"]?.ToString() };

            return rcPaths.FirstOrDefault(File.Exists);
        }
        catch
        {
            return null;
        }
    }

    // Returns a certificate for deceive-localhost.molenzwiebel.xyz, either from cache or by downloading
    // the current one from the server. The returned certificate will be valid for at least 20 days.
    public static async Task<X509Certificate2?> GetProxyCertificateAsync()
    {
        var cachedCert = Persistence.GetCachedCertificate();
        if (cachedCert is not null && cachedCert.NotAfter > DateTime.Now.AddDays(20))
        {
            Trace.WriteLine($"Cached certificate is valid until {cachedCert.NotAfter}, using cached certificate.");
            return cachedCert;
        }

        try
        {
            Trace.WriteLine("Cached certificate is missing or expiring soon, downloading new certificate.");
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SkoreLoBoostStatus", DeceiveVersion));

            var response = await httpClient.GetAsync("https://mln.cx/deceive/localhost.pfx");
            response.EnsureSuccessStatusCode();
            var certBytes = await response.Content.ReadAsByteArrayAsync();
            var cert = new X509Certificate2(certBytes);
            Persistence.SetCachedCertificate(certBytes);
            return cert;
        }
        catch (Exception ex)
        {
            // something went wrong, let's just return null and inform the user
            Trace.WriteLine($"Failed to download certificate: {ex}");
            return null;
        }
    }

    private static bool DeceiveLocalhostResolves()
    {
        try
        {
            var addresses = System.Net.Dns.GetHostAddresses(ConfigProxy.LocalhostDomain);
            if (addresses.Any(addr => addr.ToString() == "127.0.0.1"))
                return true;
        }
        catch
        {
            // intentionally empty
        }
        return false;
    }

    // Check if deceive-localhost.molenzwiebel.xyz is resolving to 127.0.0.1, and offer
    // the user to relaunch to install the necessary hosts file entry if not.
    public static void EnsureLocalhostResolution()
    {
        if (DeceiveLocalhostResolves())
            return;

        var result = MessageBox.Show(
            "O seu computador não está conseguindo resolver alguns domínios necessários. Será preciso trocar o servidor DNS ou adicionar uma linha no arquivo hosts. Veja o FAQ para mais informações (está em inglês). O SkoreLoBoost Status não vai funcionar até isso ser resolvido. Quer abrir o FAQ agora?",
            StartupHandler.DeceiveTitle,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1
        );

        if (result is DialogResult.Yes)
        {
            Process.Start("https://github.com/molenzwiebel/Deceive#FAQ");
        }

        Environment.Exit(0);
    }
}

