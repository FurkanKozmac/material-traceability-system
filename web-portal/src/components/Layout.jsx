import React from 'react';
import { Outlet, useNavigate, useLocation } from 'react-router-dom';
import api from '../api';
import { Box, Typography, Button, Drawer, List, ListItem, ListItemButton, ListItemIcon, ListItemText, CssBaseline, Avatar, AppBar, Toolbar, ButtonGroup } from '@mui/material';
import { Dashboard as DashboardIcon, Science, Inventory2, LocationOn, Sell, QrCode2, SwapHoriz, Shield, ExitToApp } from '@mui/icons-material';
import { getStoredUser } from '../auth';
import { useLanguage } from '../useLanguage';

const drawerWidth = 280;

export default function Layout() {
  const navigate = useNavigate();
  const location = useLocation();
  const activePath = location.pathname;
  const user = getStoredUser();
  const { language, setLanguage, t } = useLanguage();

  const menuItems = [
    { text: t('overview'), key: 'overview', path: '/dashboard', icon: <DashboardIcon /> },
    { text: t('chemicals'), key: 'chemicals', path: '/chemicals', icon: <Science /> },
    { text: t('batches'), key: 'batches', path: '/batches', icon: <Inventory2 /> },
    { text: t('addresses'), key: 'addresses', path: '/addresses', icon: <LocationOn /> },
    { text: t('units'), key: 'units', path: '/units', icon: <Sell /> },
    { text: t('qrLabels'), key: 'qrLabels', path: '/qr-labels', icon: <QrCode2 /> },
    { text: t('relocationTasks'), key: 'relocationTasks', path: '/relocation-tasks', icon: <SwapHoriz /> },
    { text: t('safetyAssistant.title'), key: 'safetyAssistant', path: '/safety-assistant', icon: <Shield /> },
  ];

  const handleLogout = async () => {
    const storedUser = getStoredUser();
    try {
      if (storedUser?.refreshToken) {
        await api.post('/auth/logout', { refreshToken: storedUser.refreshToken });
      }
    } catch {
    } finally {
      localStorage.removeItem('user');
      navigate('/login');
    }
  };

  const activeItem = menuItems.find(item => item.path === activePath);
  const pageTitle = activeItem ? activeItem.text : t('overview');

  return (
    <Box sx={{ display: 'flex', height: '100vh', overflow: 'hidden', backgroundColor: '#f8fafc' }}>
      <CssBaseline />
      
      {/* Top Navbar */}
      <AppBar 
        position="fixed" 
        sx={{ 
          width: `calc(100% - ${drawerWidth}px)`, 
          ml: `${drawerWidth}px`,
          backgroundColor: '#ffffff',
          color: '#1f2937',
          borderBottom: '1px solid #e5e7eb',
          boxShadow: '0 1px 2px 0 rgba(0, 0, 0, 0.05)',
          zIndex: (theme) => theme.zIndex.drawer + 1 
        }}
      >
        <Toolbar sx={{ justifyContent: 'space-between', px: 3, minHeight: 64 }}>
          <Typography variant="subtitle1" sx={{ color: '#6b7280', fontWeight: 500, fontSize: '0.875rem' }}>
            {t('system')} / {pageTitle}
          </Typography>
          
          <Box sx={{ display: 'flex', alignItems: 'center', gap: 2 }}>
            <ButtonGroup size="small" variant="outlined" sx={{ borderRadius: 2 }}>
              <Button 
                variant={language === 'tr' ? 'contained' : 'outlined'}
                onClick={() => setLanguage('tr')}
                sx={{ px: 1.25, py: 0.2, fontSize: '0.75rem', fontWeight: 700 }}
              >
                TR
              </Button>
              <Button 
                variant={language === 'en' ? 'contained' : 'outlined'}
                onClick={() => setLanguage('en')}
                sx={{ px: 1.25, py: 0.2, fontSize: '0.75rem', fontWeight: 700 }}
              >
                EN
              </Button>
            </ButtonGroup>

            <Avatar sx={{ width: 32, height: 32, fontSize: '0.875rem', fontWeight: 600, backgroundColor: '#eff6ff', color: '#2563eb' }}>
              {(user?.username || 'MTS').slice(0, 2).toUpperCase()}
            </Avatar>
            <Button 
              onClick={handleLogout} 
              startIcon={<ExitToApp />} 
              sx={{ 
                color: '#6b7280', 
                textTransform: 'none', 
                fontSize: '0.75rem', 
                fontWeight: 500,
                px: 1.5,
                py: 0.75,
                borderRadius: '6px',
                '&:hover': {
                  color: '#ef4444',
                  backgroundColor: '#fef2f2'
                }
              }}
            >
              {t('logout')}
            </Button>
          </Box>
        </Toolbar>
      </AppBar>

      {/* Sidebar */}
      <Drawer
        variant="permanent"
        sx={{
          width: drawerWidth,
          flexShrink: 0,
          [`& .MuiDrawer-paper`]: { 
            width: drawerWidth, 
            boxSizing: 'border-box',
            backgroundColor: '#ffffff',
            borderRight: '1px solid #e5e7eb',
            elevation: 0
          },
        }}
      >
        {/* Sidebar Header */}
        <Box sx={{ p: 3, display: 'flex', alignItems: 'center', gap: 1.5, minHeight: 64, borderBottom: '1px solid #f3f4f6' }}>
          <Box sx={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: '#2563eb' }} />
          <Typography variant="h6" sx={{ fontSize: '0.95rem', fontWeight: 600, color: '#1f2937', tracking: '-0.025em' }}>
            {t('systemTitle')}
          </Typography>
        </Box>

        <Box sx={{ overflow: 'auto', mt: 1 }}>
          <List sx={{ px: 1.5, display: 'flex', flexDirection: 'column', gap: 0.5 }}>
            {menuItems.map((item) => {
              const isActive = activePath === item.path;
              return (
                <ListItem key={item.key} disablePadding>
                  <ListItemButton
                    onClick={() => navigate(item.path)}
                    sx={{
                      borderRadius: '8px',
                      py: 1,
                      px: 2,
                      backgroundColor: isActive ? '#eff6ff' : 'transparent',
                      color: isActive ? '#2563eb' : '#4b5563',
                      borderLeft: isActive ? '4px solid #2563eb' : '4px solid transparent',
                      transition: 'all 0.2s',
                      '&:hover': {
                        backgroundColor: isActive ? '#eff6ff' : '#f9fafb',
                        color: '#2563eb',
                      },
                    }}
                  >
                    <ListItemIcon sx={{ minWidth: 32, color: 'inherit' }}>
                      {item.icon}
                    </ListItemIcon>
                    <ListItemText 
                      primary={item.text} 
                      slotProps={{ primary: {
                        fontSize: '0.84rem',
                        fontWeight: isActive ? 600 : 500,
                        whiteSpace: 'nowrap',
                      }}}
                    />
                  </ListItemButton>
                </ListItem>
              );
            })}
          </List>
        </Box>
      </Drawer>

      {/* Main Content Area */}
      <Box 
        component="main" 
        sx={{ 
          flexGrow: 1, 
          p: 4, 
          overflowY: 'auto', 
          height: 'calc(100vh - 64px)',
          display: 'flex',
          flexDirection: 'column',
          mt: '64px'
        }}
      >
        <Outlet />
      </Box>
    </Box>
  );
}
