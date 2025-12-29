require('dotenv').config();

/**
 * Middleware de Autenticação via API Key
 * Verifica o header 'Authorization: Bearer <API_KEY>'
 */
const authMiddleware = (req, res, next) => {
  const authHeader = req.headers.authorization;
  const apiKey = process.env.AGENT_API_KEY;

  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return res.status(401).json({ 
      success: false,
      error: 'Não autorizado: Token ausente ou formato inválido' 
    });
  }

  const token = authHeader.split(' ')[1];

  if (token !== apiKey) {
    return res.status(403).json({ 
      success: false,
      error: 'Proibido: API Key inválida' 
    });
  }

  next();
};

module.exports = authMiddleware;