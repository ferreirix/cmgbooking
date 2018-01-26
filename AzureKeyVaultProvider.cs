using Microsoft.Azure.Services.AppAuthentication;
using System.Configuration;
using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;

namespace CMGBooker
{
    public class AzureKeyVaultProvider
    {
        private static HttpClient client = new HttpClient();
        public static async Task<string> GetSecret(string id, TraceWriter log)
        {
            try
            {
                var azureServiceTokenProvider = new AzureServiceTokenProvider();
                var callback = new KeyVaultClient.AuthenticationCallback(azureServiceTokenProvider.KeyVaultTokenCallback);
                var kvClient = new KeyVaultClient(callback, client);

                var url = $"{ConfigurationManager.AppSettings["KeyVaultUri"]}secrets/{id}";

                return (await kvClient.GetSecretAsync(url)).Value;
            }
            catch (Exception ex)
            {
                log.Error(ex.ToString());
                return null;
            }
        }
    }
}
