const mysql = require('mysql2/promise');
require('dotenv').config();

// Configurações base (sem o banco de dados) para permitir a criação inicial se necessário
const dbConfig = {
  host: process.env.AGENT_DB_HOST,
  user: process.env.AGENT_DB_USER,
  password: process.env.AGENT_DB_PASS,
  waitForConnections: true,
  connectionLimit: 10,
  queueLimit: 0
};

// Pool principal que será exportado e usado pela aplicação (com o banco de dados)
const pool = mysql.createPool({
  ...dbConfig,
  database: process.env.AGENT_DB_NAME
});

// Exportamos o pool e também a configuração base para o script de inicialização
module.exports = {
  pool,
  dbConfig,
  dbName: process.env.AGENT_DB_NAME
};