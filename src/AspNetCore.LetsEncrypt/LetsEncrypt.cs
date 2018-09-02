﻿using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using AspNetCore.LetsEncrypt.Internal;
using AspNetCore.LetsEncrypt.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AspNetCore.LetsEncrypt {
    public class LetsEncrypt {
        public LetsEncryptOptions Options { get; }

        // Todo: Pass (user defined) certificate store
        // Todo: Pass (user defined) auth-key store
        public LetsEncrypt(IConfigurationSection configurationSection)
            : this(configurationSection?.Get<LetsEncryptOptions>()) { }

        public LetsEncrypt(LetsEncryptOptions options) {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            // Todo: Validate some required options
        }

        public void EnsureSslCertificate() {
            if (TestForValidCertificate(Options.Certificate, Options.RenewalBuffer, Options.Authority.Name)) {
                return;
            }

            // Todo: Inform over success (injected singleton maybe?)
            // Todo: Logging
            var errorReporter = new ErrorReporter();
            CreateAcmeHostBuilder(errorReporter)
                .Build()
                .Run();

            errorReporter.ThrowOnError();
        }

        public async Task EnsureSslCertificateAsync() {
            if (TestForValidCertificate(Options.Certificate, Options.RenewalBuffer, Options.Authority.Name)) {
                return;
            }

            var errorReporter = new ErrorReporter();
            await CreateAcmeHostBuilder(errorReporter)
                .Build()
                .RunAsync();

            errorReporter.ThrowOnError();
        }

        // Todo: Add ability to customize the IWebHostBuilder
        private IWebHostBuilder CreateAcmeHostBuilder(ErrorReporter errorReporter) =>
            new WebHostBuilder()
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseKestrel(options => options.ListenAnyIP(80))
                .ConfigureServices(services => {
                    services.AddSingleton(Options);
                    services.AddSingleton(errorReporter);
                })
                .UseStartup<HostStartup>();


        private static bool TestForValidCertificate(Certificate certificate, TimeSpan renewalBuffer, string authorityName) {
            if (!File.Exists(certificate.Filename)) {
                // Certificate does not exist yet
                return false;
            }

            // Certificate exists already
            var existingCertificates = new X509Certificate2Collection();
            existingCertificates.Import(certificate.Filename, certificate.Password, X509KeyStorageFlags.PersistKeySet);

            // Test if a certificate is issued by the specified authority and whether it's not expired
            return existingCertificates
                .Cast<X509Certificate2>()
                .Where(c => c.Issuer.Equals(authorityName, StringComparison.InvariantCultureIgnoreCase))
                .Any(c => ( c.NotAfter - renewalBuffer ) > DateTime.Now && c.NotBefore < DateTime.Now);
        }
    }
}
