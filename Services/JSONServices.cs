using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using WatchCollection.Models;

namespace WatchCollection.Services;

/// <summary>
/// Service d'accès au serveur JSON académique distant.
/// Conforme au cahier des charges section 2.1 (chargement depuis serveur distant)
/// et à la section 4 (sauvegarde de paramètres).
///
/// Conçu pour être tolérant aux pannes : tous les modes d'échec
/// (réseau indisponible, JSON corrompu, timeout, coupure de flux) sont capturés
/// et exposés via la propriété LastError plutôt que de propager des exceptions.
/// </summary>
public class JSONServices
{
    private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private const string BaseUrl = "http://185.157.245.38:8080/json";
    private const string FileName = "MyWatches.json";

    /// <summary>
    /// Dernière erreur rencontrée, exploitable par les appelants pour informer l'utilisateur.
    /// Null si la dernière opération s'est terminée avec succès.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Récupère la liste des montres depuis le serveur JSON distant.
    /// Renvoie une liste vide en cas d'erreur (réseau, JSON corrompu, timeout, etc.).
    /// L'erreur précise est exposée via <see cref="LastError"/>.
    /// </summary>
    public async Task<List<Watch>> GetWatchesAsync()
    {
        LastError = null;
        var url = $"{BaseUrl}?FileName={FileName}";

        try
        {
            using var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Le serveur a renvoyé le statut {(int)response.StatusCode} ({response.StatusCode}).";
                return [];
            }

            await using var contentStream = await response.Content.ReadAsStreamAsync();

            // Validation : un stream vide ne peut pas être un JSON valide
            if (response.Content.Headers.ContentLength == 0)
            {
                LastError = "Le serveur a renvoyé une réponse vide.";
                return [];
            }

            try
            {
                var watches = await JsonSerializer.DeserializeAsync<List<Watch>>(
                    contentStream,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return watches ?? [];
            }
            catch (JsonException ex)
            {
                // Cas 'In JSON corrompu' explicitement géré : le contenu n'est pas du JSON valide
                LastError = $"Fichier JSON corrompu sur le serveur : {ex.Message}";
                return [];
            }
        }
        catch (TaskCanceledException)
        {
            // Timeout dépassé (10 secondes par défaut sur _httpClient)
            LastError = "Délai d'attente dépassé : le serveur ne répond pas.";
            return [];
        }
        catch (HttpRequestException ex)
        {
            // Réseau indisponible, serveur injoignable, certificat invalide, etc.
            LastError = $"Erreur réseau : {ex.Message}";
            return [];
        }
        catch (IOException ex)
        {
            // Lecture du flux interrompue (connexion coupée en cours)
            LastError = $"Erreur de lecture du flux réseau : {ex.Message}";
            return [];
        }
        catch (Exception ex)
        {
            // Filet de sécurité pour les exceptions inattendues
            LastError = $"Erreur inattendue : {ex.GetType().Name} - {ex.Message}";
            return [];
        }
    }

    /// <summary>
    /// Sauvegarde la liste des montres sur le serveur JSON distant.
    /// Retourne true si la sauvegarde a réussi, false sinon.
    /// L'erreur précise est exposée via <see cref="LastError"/>.
    /// </summary>
    public async Task<bool> SetWatchesAsync(List<Watch> watches)
    {
        LastError = null;

        try
        {
            using var memoryStream = new MemoryStream();
            await JsonSerializer.SerializeAsync(memoryStream, watches);
            memoryStream.Position = 0;

            var fileContent = new StreamContent(memoryStream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/json") }
            };

            using var content = new MultipartFormDataContent { { fileContent, "file", FileName } };
            using var response = await _httpClient.PostAsync(BaseUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                LastError = $"Le serveur a refusé la sauvegarde (statut {(int)response.StatusCode}).";
                return false;
            }

            return true;
        }
        catch (TaskCanceledException)
        {
            LastError = "Délai d'attente dépassé pendant la sauvegarde.";
            return false;
        }
        catch (HttpRequestException ex)
        {
            LastError = $"Erreur réseau : {ex.Message}";
            return false;
        }
        catch (JsonException ex)
        {
            LastError = $"Erreur de sérialisation JSON : {ex.Message}";
            return false;
        }
        catch (Exception ex)
        {
            LastError = $"Erreur inattendue : {ex.GetType().Name} - {ex.Message}";
            return false;
        }
    }
}