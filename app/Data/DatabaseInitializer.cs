using System;
using System.Threading.Tasks;
using MySqlConnector;
using Dapper;

namespace NeskAgent.Data
{
    public class DatabaseInitializer
    {
        private readonly string _connectionString;
        private readonly string _dbName;

        public DatabaseInitializer(string connectionString, string dbName)
        {
            _connectionString = connectionString;
            _dbName = dbName;
        }

        public async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine("⏳ Verificando/Criando banco de dados e tabelas...");

                // 1. Conecta sem especificar o banco de dados para criá-lo se não existir
                using (var connection = new MySqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await connection.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{_dbName}`;");
                    Console.WriteLine($"✅ Banco de dados \"{_dbName}\" verificado/criado.");
                }

                // 2. Conecta ao banco de dados específico para criar as tabelas
                var dbConnectionString = $"{_connectionString};Database={_dbName}";
                using (var connection = new MySqlConnection(dbConnectionString))
                {
                    await connection.OpenAsync();

                    const string createProxiesTable = @"
                        CREATE TABLE IF NOT EXISTS proxies (
                            id VARCHAR(36) PRIMARY KEY,
                            domain VARCHAR(255) UNIQUE NOT NULL,
                            target_host VARCHAR(255) NOT NULL,
                            target_port INT,
                            enabled TINYINT(1) DEFAULT 0,
                            created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                    ";

                    await connection.ExecuteAsync(createProxiesTable);
                    Console.WriteLine("✅ Tabela \"proxies\" verificada/criada com sucesso.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Erro ao inicializar banco de dados: {ex.Message}");
                throw;
            }
        }
    }
}
