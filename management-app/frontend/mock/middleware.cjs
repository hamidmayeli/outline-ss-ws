module.exports = function (req, res, next) {
  if (req.path === '/api/v1/auth/login') {
    return res.status(200).json({ token: 'mock-token' });
  }

  next();
};
