import React from 'react';
import { Box, Button, Typography, Container, Grid, Paper } from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { QrCode, Layers, ShieldCheck, Activity, ArrowRight, Truck } from 'lucide-react';
import { useLanguage } from '../useLanguage';

export default function Landing() {
  const navigate = useNavigate();
  const { t } = useLanguage();

  const features = [
    {
      icon: <QrCode size={40} color="#2563eb" />,
      title: t('featureQr'), description: t('featureQrDesc')
    },
    {
      icon: <Layers size={40} color="#16a34a" />,
      title: t('featureStock'), description: t('featureStockDesc')
    },
    {
      icon: <ShieldCheck size={40} color="#ea580c" />,
      title: t('featureSecurity'), description: t('featureSecurityDesc')
    },
    {
      icon: <Activity size={40} color="#9333ea" />,
      title: t('featureInsights'), description: t('featureInsightsDesc')
    },
  ];

  return (
    <Box sx={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', backgroundColor: '#f8fafc', overflowX: 'hidden' }}>
      
      {/* Navbar */}
      <Box sx={{ 
        px: { xs: 2, md: 8 }, py: 3, 
        display: 'flex', justifyContent: 'space-between', alignItems: 'center',
        background: 'rgba(255, 255, 255, 0.8)', backdropFilter: 'blur(12px)',
        borderBottom: '1px solid rgba(0,0,0,0.05)', position: 'sticky', top: 0, zIndex: 50
      }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5 }}>
          <Box sx={{ width: 40, height: 40, borderRadius: 2, background: 'linear-gradient(135deg, #1e3a8a, #3b82f6)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
            <Layers color="white" size={24} />
          </Box>
          <Typography variant="h6" sx={{ fontWeight: 800, color: '#0f172a', letterSpacing: '-0.5px' }}>MTS</Typography>
        </Box>
        <Button 
          variant="contained" 
          onClick={() => navigate('/login')}
          endIcon={<ArrowRight size={18} />}
          sx={{ 
            borderRadius: 8, textTransform: 'none', fontWeight: 600, px: 3,
            background: 'linear-gradient(to right, #2563eb, #3b82f6)',
            boxShadow: '0 4px 14px 0 rgba(37, 99, 235, 0.39)'
          }}
        >
          {t('landingLogin')}
        </Button>
      </Box>

      {/* Hero Section */}
      <Container maxWidth="lg" sx={{ flexGrow: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center', py: { xs: 8, md: 12 }, position: 'relative' }}>
        
        {/* Background Gradients */}
        <Box sx={{ position: 'absolute', top: '10%', right: '-10%', width: '400px', height: '400px', borderRadius: '50%', background: 'radial-gradient(circle, rgba(59,130,246,0.15) 0%, rgba(0,0,0,0) 70%)', zIndex: 0 }} />
        <Box sx={{ position: 'absolute', bottom: '10%', left: '-5%', width: '300px', height: '300px', borderRadius: '50%', background: 'radial-gradient(circle, rgba(147,51,234,0.1) 0%, rgba(0,0,0,0) 70%)', zIndex: 0 }} />

        <Grid container spacing={6} alignItems="center" sx={{ position: 'relative', zIndex: 1 }}>
          <Grid size={{ xs: 12, md: 7 }}>
            <Box sx={{ 
              display: 'inline-block', mb: 2, px: 2, py: 0.5, 
              backgroundColor: '#eff6ff', color: '#1d4ed8', borderRadius: 8, 
              fontWeight: 600, fontSize: '0.875rem' 
            }}>
              {t('landingReady')} ✨
            </Box>
            <Typography variant="h2" sx={{ fontWeight: 800, color: '#0f172a', mb: 3, letterSpacing: '-1px', lineHeight: 1.1 }}>
              {t('landingTitle')}
              <Typography component="span" variant="h2" sx={{ fontWeight: 800, background: 'linear-gradient(135deg, #2563eb, #9333ea)', WebkitBackgroundClip: 'text', WebkitTextFillColor: 'transparent' }}>
                Malzeme İzlenebilirliği
              </Typography>
            </Typography>
            <Typography variant="h6" sx={{ color: '#475569', mb: 4, fontWeight: 400, maxWidth: '90%', lineHeight: 1.6 }}>
              {t('landingDescription')}
            </Typography>
            <Box sx={{ display: 'flex', gap: 2 }}>
              <Button 
                variant="contained" 
                onClick={() => navigate('/login')}
                size="large"
                sx={{ 
                  borderRadius: 2, textTransform: 'none', fontWeight: 600, px: 4, py: 1.5,
                  background: '#0f172a', '&:hover': { background: '#1e293b' }
                }}
              >
                {t('accessSystem')}
              </Button>
              <Button 
                variant="outlined" 
                onClick={() => navigate('/login')} // Later can point to docs
                size="large"
                sx={{ 
                  borderRadius: 2, textTransform: 'none', fontWeight: 600, px: 4, py: 1.5,
                  borderColor: '#cbd5e1', color: '#475569', '&:hover': { borderColor: '#94a3b8', backgroundColor: '#f1f5f9' }
                }}
              >
                {t('viewDocs')}
              </Button>
            </Box>
          </Grid>
          
          <Grid size={{ xs: 12, md: 5 }}>
            {/* Abstract UI representation */}
            <Paper sx={{ 
              p: 4, borderRadius: 4, background: 'rgba(255, 255, 255, 0.7)', backdropFilter: 'blur(20px)',
              border: '1px solid rgba(255,255,255,0.8)', boxShadow: '0 25px 50px -12px rgba(0,0,0,0.1)',
              position: 'relative', overflow: 'hidden'
            }}>
              <Box sx={{ display: 'flex', justifyContent: 'space-between', mb: 3, alignItems: 'center' }}>
                <Typography variant="subtitle2" sx={{ fontWeight: 700, color: '#64748b' }}>{t('liveFeed')}</Typography>
                <Box sx={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: '#22c55e', boxShadow: '0 0 10px #22c55e', animation: 'pulse 2s infinite' }} />
              </Box>
              
              {/* Dummy data rows */}
              {[
                { title: t('liveTanker'), time: `2 ${t('minutesAgo')}`, icon: <Truck size={18} color="#2563eb"/>, bg: '#eff6ff' },
                { title: t('liveBatch'), time: `14 ${t('minutesAgo')}`, icon: <Activity size={18} color="#16a34a"/>, bg: '#dcfce7' },
                { title: t('liveStock'), time: `1 ${t('hourAgo')}`, icon: <ShieldCheck size={18} color="#ea580c"/>, bg: '#ffedd5' },
              ].map((item, i) => (
                <Box key={i} sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2, p: 2, backgroundColor: '#ffffff', borderRadius: 2, border: '1px solid #f1f5f9' }}>
                  <Box sx={{ p: 1.5, borderRadius: 2, backgroundColor: item.bg }}>{item.icon}</Box>
                  <Box sx={{ flexGrow: 1 }}>
                    <Typography variant="body2" sx={{ fontWeight: 600, color: '#334155' }}>{item.title}</Typography>
                    <Typography variant="caption" sx={{ color: '#94a3b8' }}>{item.time}</Typography>
                  </Box>
                </Box>
              ))}
            </Paper>
          </Grid>
        </Grid>
      </Container>

      {/* Features Section */}
      <Box sx={{ backgroundColor: '#ffffff', py: 10, borderTop: '1px solid #f1f5f9' }}>
        <Container maxWidth="lg">
          <Typography variant="h3" sx={{ textAlign: 'center', fontWeight: 800, color: '#0f172a', mb: 8, letterSpacing: '-0.5px' }}>
            {t('landingFeaturesTitle')}
          </Typography>
          <Grid container spacing={4}>
            {features.map((feature, idx) => (
              <Grid size={{ xs: 12, sm: 6, md: 3 }} key={idx}>
                <Paper sx={{ p: 4, height: '100%', borderRadius: 4, border: '1px solid #f1f5f9', boxShadow: 'none', transition: 'all 0.3s', '&:hover': { boxShadow: '0 20px 25px -5px rgba(0,0,0,0.05)', transform: 'translateY(-5px)' } }}>
                  <Box sx={{ mb: 3 }}>{feature.icon}</Box>
                  <Typography variant="h6" sx={{ fontWeight: 700, mb: 1, color: '#1e293b' }}>{feature.title}</Typography>
                  <Typography variant="body2" sx={{ color: '#64748b', lineHeight: 1.6 }}>{feature.description}</Typography>
                </Paper>
              </Grid>
            ))}
          </Grid>
        </Container>
      </Box>
      
      {/* Add keyframes globally for pulse */}
      <style>
        {`
          @keyframes pulse {
            0% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(34, 197, 94, 0.7); }
            70% { transform: scale(1); box-shadow: 0 0 0 10px rgba(34, 197, 94, 0); }
            100% { transform: scale(0.95); box-shadow: 0 0 0 0 rgba(34, 197, 94, 0); }
          }
        `}
      </style>
    </Box>
  );
}
