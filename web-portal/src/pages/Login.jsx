import React, { useState } from 'react';
import { Box, Button, TextField, Typography, Container, Alert, Paper, InputAdornment, IconButton, CircularProgress, ButtonGroup } from '@mui/material';
import { useNavigate, Navigate } from 'react-router-dom';
import { User, Lock, Eye, EyeOff, LogIn } from 'lucide-react';
import api from '../api';
import { getStoredUser } from '../auth';
import { useLanguage } from '../useLanguage';

export default function Login() {
  const { language, setLanguage, t } = useLanguage();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const navigate = useNavigate();

  // If already logged in, redirect to dashboard immediately
  const storedUser = getStoredUser();
  if (storedUser) {
    return <Navigate to="/dashboard" replace />;
  }

  const handleLogin = async (e) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const res = await api.post('/auth/login-web', { username, password, authSource: 'DB' });
      
      localStorage.setItem('user', JSON.stringify(res.data));
      
      // Artificial delay for smooth transition effect
      setTimeout(() => {
        navigate('/dashboard');
      }, 500);
    } catch (err) {
      setError(err.response?.data?.message || t('loginFailed'));
      setLoading(false);
    }
  };

  return (
    <Box 
      sx={{ 
        minHeight: '100vh', 
        display: 'flex', 
        alignItems: 'center', 
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #1e3c72 0%, #2a5298 100%)',
        position: 'relative',
        overflow: 'hidden'
      }}
    >
      {/* Background abstract circles */}
      <Box sx={{ position: 'absolute', top: '-10%', left: '-5%', width: '300px', height: '300px', borderRadius: '50%', background: 'rgba(255, 255, 255, 0.05)', filter: 'blur(40px)' }} />
      <Box sx={{ position: 'absolute', bottom: '-10%', right: '-5%', width: '400px', height: '400px', borderRadius: '50%', background: 'rgba(255, 255, 255, 0.05)', filter: 'blur(60px)' }} />

      <Container component="main" maxWidth="xs" sx={{ position: 'relative', zIndex: 1 }}>
        <Paper 
          elevation={24} 
          sx={{ 
            p: { xs: 4, md: 5 }, 
            width: '100%', 
            borderRadius: 4,
            background: 'rgba(255, 255, 255, 0.95)',
            backdropFilter: 'blur(20px)',
            border: '1px solid rgba(255, 255, 255, 0.2)',
            display: 'flex',
            flexDirection: 'column',
            alignItems: 'center',
            boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.5)'
          }}
        >
          {/* Language Toggle Bar */}
          <Box sx={{ width: '100%', display: 'flex', justifyContent: 'flex-end', mb: 1 }}>
            <ButtonGroup size="small" variant="outlined" sx={{ borderRadius: 2 }}>
              <Button 
                variant={language === 'tr' ? 'contained' : 'outlined'}
                onClick={() => setLanguage('tr')}
                sx={{ px: 1.5, py: 0.25, fontSize: '0.75rem', fontWeight: 700 }}
              >
                TR
              </Button>
              <Button 
                variant={language === 'en' ? 'contained' : 'outlined'}
                onClick={() => setLanguage('en')}
                sx={{ px: 1.5, py: 0.25, fontSize: '0.75rem', fontWeight: 700 }}
              >
                EN
              </Button>
            </ButtonGroup>
          </Box>

          {/* Logo / Header */}
          <Box 
            sx={{ 
              width: 60, height: 60, 
              borderRadius: '16px', 
              background: 'linear-gradient(135deg, #2563eb 0%, #1e40af 100%)',
              display: 'flex', alignItems: 'center', justifyContent: 'center',
              mb: 2,
              boxShadow: '0 10px 15px -3px rgba(37, 99, 235, 0.4)'
            }}
          >
            <LogIn color="white" size={32} />
          </Box>
          
          <Typography component="h1" variant="h4" sx={{ fontWeight: 800, color: '#1e293b', mb: 1, letterSpacing: '-0.5px' }}>
            {t('welcome')}
          </Typography>
          <Typography variant="body2" sx={{ color: '#64748b', mb: 4, textAlign: 'center' }}>
            {t('systemTitle')}
          </Typography>

          {error && (
            <Alert severity="error" sx={{ width: '100%', mb: 3, borderRadius: 2 }}>
              {error}
            </Alert>
          )}

          <Box component="form" onSubmit={handleLogin} sx={{ width: '100%' }}>
            {/* Custom styled TextField */}
            <TextField
              margin="normal"
              required
              fullWidth
              id="username"
              label={t('username')}
              name="username"
              autoComplete="username"
              autoFocus
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              slotProps={{ input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <User size={20} color="#94a3b8" />
                  </InputAdornment>
                ),
                sx: { borderRadius: 2, backgroundColor: '#f8fafc', '& fieldset': { borderColor: '#e2e8f0' } }
              }}}
              variant="outlined"
            />

            <TextField
              margin="normal"
              required
              fullWidth
              name="password"
              label={t('password')}
              type={showPassword ? 'text' : 'password'}
              id="password"
              autoComplete="current-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              slotProps={{ input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <Lock size={20} color="#94a3b8" />
                  </InputAdornment>
                ),
                endAdornment: (
                  <InputAdornment position="end">
                    <IconButton
                      aria-label="Parola görünürlüğünü değiştir"
                      onClick={() => setShowPassword(!showPassword)}
                      edge="end"
                      size="small"
                    >
                      {showPassword ? <EyeOff size={18} color="#94a3b8" /> : <Eye size={18} color="#94a3b8" />}
                    </IconButton>
                  </InputAdornment>
                ),
                sx: { borderRadius: 2, backgroundColor: '#f8fafc', '& fieldset': { borderColor: '#e2e8f0' } }
              }}}
            />

            <Button
              type="submit"
              fullWidth
              variant="contained"
              disabled={loading}
              sx={{ 
                mt: 3, mb: 2, py: 1.5, 
                borderRadius: 2,
                textTransform: 'none',
                fontSize: '1.05rem',
                fontWeight: 600,
                background: 'linear-gradient(to right, #2563eb, #3b82f6)',
                boxShadow: '0 4px 14px 0 rgba(37, 99, 235, 0.39)',
                transition: 'all 0.2s',
                '&:hover': {
                  background: 'linear-gradient(to right, #1d4ed8, #2563eb)',
                  transform: 'translateY(-1px)',
                  boxShadow: '0 6px 20px rgba(37, 99, 235, 0.4)'
                }
              }}
            >
              {loading ? <CircularProgress size={24} color="inherit" /> : t('login')}
            </Button>
          </Box>
        </Paper>
      </Container>
    </Box>
  );
}
