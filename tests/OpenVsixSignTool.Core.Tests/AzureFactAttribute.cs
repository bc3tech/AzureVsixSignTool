namespace OpenVsixSignTool.Core.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;

    using Xunit;

    public sealed class AzureFactAttribute : FactAttribute
    {
        public AzureFactAttribute()
        {
            if (TestAzureCredentials.Credentials == null)
            {
                this.Skip = "Test Azure credentials are not set up correctly. " +
                    "Please see the README for more information.";
            }
        }

        //Shadow the Skip as get only so it isn't set when an instance of the
        //attribute is declared
        public new string Skip
        {
            get => base.Skip;
            private set => base.Skip = value;
        }
    }

    public class TestAzureCredentials
    {
        public static TestAzureCredentials Credentials { get; }

        static TestAzureCredentials()
        {
            try
            {
                var contents = File.ReadAllText(@"private\azure-creds.json");
                Credentials = JsonSerializer.Deserialize<TestAzureCredentials>(contents);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AzureKeyVaultUrl { get; set; }
        public string AzureKeyVaultCertificateName { get; set; }
    }
}
