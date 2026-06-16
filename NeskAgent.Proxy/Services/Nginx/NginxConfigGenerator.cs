using System;
using System.IO;
using System.Text;

namespace NeskAgent.Proxy.Services.Nginx
{
    public class NginxConfigGenerator
    {
        /// <summary>
        /// Generates the nginx configuration based on the proxy parameters.
        /// When sslAvailable is true but the certificate doesn't exist yet, 
        /// generates HTTP config to avoid nginx loading failures.
        /// </summary>
        public string Generate(string domain, string targetHost, int targetPort, bool enabled, bool sslAvailable)
        {
            if (!enabled)
            {
                return GenerateDisabledConfig(domain, sslAvailable);
            }

            // Only generate HTTPS config if SSL is available AND certificate exists
            if (sslAvailable && HasCertificate(domain))
            {
                return GenerateHttpsConfig(domain, targetHost, targetPort);
            }

            return GenerateHttpConfig(domain, targetHost, targetPort);
        }

        private static bool HasCertificate(string domain)
        {
            return File.Exists($"/etc/letsencrypt/live/{domain}/fullchain.pem");
        }

        private string GenerateHttpConfig(string domain, string targetHost, int targetPort)
        {
            var sb = new StringBuilder();
            sb.AppendLine("server {");
            sb.AppendLine("    listen 80;");
            sb.AppendLine($"    server_name {domain};");
            sb.AppendLine();
            sb.AppendLine("    location / {");
            sb.AppendLine($"        proxy_pass http://{targetHost}:{targetPort};");
            sb.AppendLine("        proxy_set_header Host $host;");
            sb.AppendLine("        proxy_set_header X-Real-IP $remote_addr;");
            sb.AppendLine("        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
            sb.AppendLine("        proxy_set_header X-Forwarded-Proto $scheme;");
            sb.AppendLine("        proxy_http_version 1.1;");
            sb.AppendLine("        proxy_set_header Upgrade $http_upgrade;");
            sb.AppendLine("        proxy_set_header Connection \"upgrade\";");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        private string GenerateHttpsConfig(string domain, string targetHost, int targetPort)
        {
            var sb = new StringBuilder();
            sb.AppendLine("server {");
            sb.AppendLine("    listen 80;");
            sb.AppendLine($"    server_name {domain};");
            sb.AppendLine($"    return 301 https://$host$request_uri;");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("server {");
            sb.AppendLine("    listen 443 ssl http2;");
            sb.AppendLine($"    server_name {domain};");
            sb.AppendLine();
            sb.AppendLine($"    ssl_certificate /etc/letsencrypt/live/{domain}/fullchain.pem;");
            sb.AppendLine($"    ssl_certificate_key /etc/letsencrypt/live/{domain}/privkey.pem;");
            sb.AppendLine("    ssl_protocols TLSv1.2 TLSv1.3;");
            sb.AppendLine("    ssl_ciphers HIGH:!aNULL:!MD5;");
            sb.AppendLine();
            sb.AppendLine("    location / {");
            sb.AppendLine($"        proxy_pass http://{targetHost}:{targetPort};");
            sb.AppendLine("        proxy_set_header Host $host;");
            sb.AppendLine("        proxy_set_header X-Real-IP $remote_addr;");
            sb.AppendLine("        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;");
            sb.AppendLine("        proxy_set_header X-Forwarded-Proto $scheme;");
            sb.AppendLine("        proxy_http_version 1.1;");
            sb.AppendLine("        proxy_set_header Upgrade $http_upgrade;");
            sb.AppendLine("        proxy_set_header Connection \"upgrade\";");
            sb.AppendLine("    }");
            sb.AppendLine("}");
            return sb.ToString();
        }

        /// <summary>
        /// When disabled, preserves the SSL block if the certificate exists.
        /// This allows the deactivated page to be served over HTTPS.
        /// Matches V2 behavior: =503 with Cache-Control headers.
        /// </summary>
        private string GenerateDisabledConfig(string domain, bool sslAvailable)
        {
            var sb = new StringBuilder();
            
            // Check if we have a valid SSL certificate for this domain
            bool hasCert = !string.IsNullOrEmpty(domain) && File.Exists($"/etc/letsencrypt/live/{domain}/fullchain.pem");

            // 443 block if SSL is available and cert exists
            if (sslAvailable && hasCert)
            {
                sb.AppendLine("server {");
                sb.AppendLine("    listen 443 ssl http2;");
                sb.AppendLine($"    server_name {domain};");
                sb.AppendLine();
                sb.AppendLine($"    ssl_certificate /etc/letsencrypt/live/{domain}/fullchain.pem;");
                sb.AppendLine($"    ssl_certificate_key /etc/letsencrypt/live/{domain}/privkey.pem;");
                sb.AppendLine();
                sb.AppendLine("    location / {");
                sb.AppendLine("        root /var/www/html;");
                sb.AppendLine("        try_files /nesk_deactivated.html =503;");
                sb.AppendLine("        add_header Cache-Control \"no-store, no-cache, must-revalidate\";");
                sb.AppendLine("    }");
                sb.AppendLine("}");
                sb.AppendLine();
            }

            // Always add the 80 block
            sb.AppendLine("server {");
            sb.AppendLine("    listen 80;");
            sb.AppendLine($"    server_name {domain};");
            sb.AppendLine();
            sb.AppendLine("    location / {");
            sb.AppendLine("        root /var/www/html;");
            sb.AppendLine("        try_files /nesk_deactivated.html =503;");
            sb.AppendLine("        add_header Cache-Control \"no-store, no-cache, must-revalidate\";");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
