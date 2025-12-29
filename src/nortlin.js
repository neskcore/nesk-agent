const express = require('express');
const cors = require('cors');
require('dotenv').config();

const apiRoutes = require('./api/routes');
const initDatabase = require('./db/init');

const app = express();
const port = process.env.PORT || 4000;

// Middlewares Globais
app.use(cors());
app.use(express.json());

// Check de saúde público
app.get('/health', (req, res) => {
  res.json({ 
    success: true, 
    message: 'Nesk Agent está operante', 
    timestamp: new Date() 
  });
});

// Rotas da API (Autenticação interna no routes.js)
app.use('/api', apiRoutes);

// Tratamento Global de Erros
app.use((err, req, res, next) => {
  console.error(`[SERVER ERROR] ${err.stack}`);
  const status = err.status || 500;
  res.status(status).json({
    success: false,
    error: process.env.NODE_ENV === 'production' ? 'Erro interno no servidor' : err.message
  });
});

/**
 * Inicialização do Agente
 */
async function startServer() {
  try {
    // Inicializa o banco de dados antes de subir o servidor
    await initDatabase();
    
    app.listen(port, () => {
      console.log('-------------------------------------------');
      console.log(`🚀 Nesk Agent iniciado na porta ${port}`);
      console.log(`📅 Data: ${new Date().toLocaleString()}`);
      console.log('-------------------------------------------');
    });
  } catch (error) {
    console.error('❌ Falha crítica ao iniciar o servidor:', error);
    process.exit(1);
  }
}

startServer();