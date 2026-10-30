import React, { useCallback, useEffect, useState, memo } from 'react';
import { Box, Typography, Grid, Paper, Table, TableBody, TableCell, TableHead, TableRow, Chip, LinearProgress } from '@mui/material';
import { Layers, AlertTriangle, CheckCircle, Clock } from 'lucide-react';
import api from '../api';
import useWebSocket from '../hooks/useWebSocket';
import useSignalR from '../hooks/useSignalR';
import { useLanguage } from '../useLanguage';
import { formatDaysLeft, formatShownOfTotal, lowStockSeverity } from '../domain/dashboardAlerts';

const StatCard = memo(({ title, value, icon, color, bgColor }) => (
  <Paper sx={{ p: 3, display: 'flex', alignItems: 'center', justifyContent: 'space-between', borderRadius: 2, boxShadow: '0 1px 3px 0 rgba(0,0,0,0.1)' }}>
    <Box>
      <Typography variant="subtitle2" sx={{ color: '#6b7280', fontWeight: 600, mb: 1, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
        {title}
      </Typography>
      <Typography variant="h3" sx={{ color: '#1f2937', fontWeight: 700 }}>
        {value}
      </Typography>
    </Box>
    <Box sx={{ p: 2, borderRadius: '50%', backgroundColor: bgColor, color: color, display: 'flex' }}>
      {icon}
    </Box>
  </Paper>
));

const AlertPanel = memo(({ title, shown, total, emptyText, headers, children }) => (
  <Paper sx={{ height: '100%', borderRadius: 2, boxShadow: '0 1px 3px 0 rgba(0,0,0,0.1)', overflow: 'hidden' }}>
    <Box sx={{ p: 2, borderBottom: '1px solid #e5e7eb', backgroundColor: '#f8fafc', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
      <Typography variant="h6" sx={{ fontWeight: 600, color: '#1f2937' }}>{title}</Typography>
      <Chip label={formatShownOfTotal(shown, total)} size="small" sx={{ fontWeight: 600 }} />
    </Box>
    {shown === 0 ? (
      <Typography variant="body2" sx={{ p: 3, color: '#64748b', textAlign: 'center' }}>{emptyText}</Typography>
    ) : (
      <Table size="small">
        <TableHead>
          <TableRow>
            {headers.map((header) => <TableCell key={header} sx={{ fontWeight: 600 }}>{header}</TableCell>)}
          </TableRow>
        </TableHead>
        <TableBody>{children}</TableBody>
      </Table>
    )}
  </Paper>
));

const Dashboard = memo(() => {
  const { t, language } = useLanguage();
  const [stats, setStats] = useState({
    totalAvailable: 0,
    totalConsumed: 0,
    pendingReUse: 0,
    expired: 0,
    recentActivity: [],
    alerts: { nearExpiryDays: 30, nearExpiryTotal: 0, nearExpiry: [], lowStockTotal: 0, lowStock: [] },
    loading: true
  });

  const fetchDashboardData = useCallback(async () => {
    try {
      const [aggregateResponse, pendingResponse, recentResponse, alertsResponse] = await Promise.all([
        api.get('/dashboard/aggregate'),
        api.get('/dashboard/pending'),
        api.get('/dashboard/recent-consumption'),
        api.get('/dashboard/alerts'),
      ]);
      const aggregates = aggregateResponse.data;
      const recentActivity = recentResponse.data;
      setStats({ totalAvailable: aggregates.availableInStock, totalConsumed: aggregates.totalConsumed,
        pendingReUse: pendingResponse.data.count, expired: aggregates.blockedExpired,
        recentActivity, alerts: alertsResponse.data, loading: false });
    } catch (err) {
      console.error('Failed to fetch dashboard stats', err);
      setStats(prev => ({ ...prev, loading: false }));
    }
  }, []);

  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  useWebSocket(() => {
    fetchDashboardData();
  });

  useSignalR('BatchCreated', () => {
    fetchDashboardData();
  });

  if (stats.loading) {
    return <Box sx={{ p: 4 }}><LinearProgress /></Box>;
  }

  return (
    <Box sx={{ pb: 4 }}>
      {/* Top Stat Cards */}
      <Grid container spacing={3} sx={{ mb: 4 }}>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard 
            title={t('inStockUnits')}
            value={stats.totalAvailable} 
            icon={<Layers size={28} />} 
            color="#0ea5e9" 
            bgColor="#e0f2fe" 
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard 
            title={t('totalConsumed')} 
            value={stats.totalConsumed} 
            icon={<CheckCircle size={28} />} 
            color="#16a34a" 
            bgColor="#dcfce7" 
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard 
            title={t('pendingOps')} 
            value={stats.pendingReUse} 
            icon={<Clock size={28} />} 
            color="#ea580c" 
            bgColor="#ffedd5" 
          />
        </Grid>
        <Grid size={{ xs: 12, sm: 6, md: 3 }}>
          <StatCard 
            title={t('expiredBlocked')}
            value={stats.expired} 
            icon={<AlertTriangle size={28} />} 
            color="#dc2626" 
            bgColor="#fee2e2" 
          />
        </Grid>
      </Grid>

      {/* Near-expiry and low-stock alerts */}
      <Grid container spacing={3} sx={{ mb: 4 }}>
        <Grid size={{ xs: 12, md: 6 }}>
          <AlertPanel
            title={t('nearExpiryBatches').replace('{days}', stats.alerts.nearExpiryDays)}
            shown={stats.alerts.nearExpiry.length}
            total={stats.alerts.nearExpiryTotal}
            emptyText={t('noNearExpiry').replace('{days}', stats.alerts.nearExpiryDays)}
            headers={[t('batchNo'), t('chemical'), t('expirationDate'), t('daysLeft'), t('activeUnits')]}
          >
            {stats.alerts.nearExpiry.map((batch) => (
              <TableRow key={batch.batchId} hover>
                <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>{batch.batchNo}</TableCell>
                <TableCell>{batch.chemicalName}</TableCell>
                <TableCell>{new Date(batch.expirationDate).toLocaleDateString(language === 'tr' ? 'tr-TR' : 'en-US')}</TableCell>
                <TableCell>
                  <Chip label={formatDaysLeft(batch.daysLeft, t)} size="small" sx={{ backgroundColor: '#ffedd5', color: '#9a3412', fontWeight: 600 }} />
                </TableCell>
                <TableCell>{batch.activeUnits}</TableCell>
              </TableRow>
            ))}
          </AlertPanel>
        </Grid>
        <Grid size={{ xs: 12, md: 6 }}>
          <AlertPanel
            title={t('lowStockChemicals')}
            shown={stats.alerts.lowStock.length}
            total={stats.alerts.lowStockTotal}
            emptyText={t('noLowStock')}
            headers={[t('chemicalCode'), t('chemicalName'), t('activeUnits'), t('stockThreshold'), '']}
          >
            {stats.alerts.lowStock.map((chemical) => {
              const isOut = lowStockSeverity(chemical.activeUnits) === 'out';
              return (
                <TableRow key={chemical.chemicalId} hover>
                  <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>{chemical.chemicalCode}</TableCell>
                  <TableCell>{chemical.name}</TableCell>
                  <TableCell>{chemical.activeUnits}</TableCell>
                  <TableCell>{chemical.minStockThreshold}</TableCell>
                  <TableCell>
                    <Chip
                      label={isOut ? t('outOfStock') : t('lowStockBadge')}
                      size="small"
                      sx={{ backgroundColor: isOut ? '#fee2e2' : '#ffedd5', color: isOut ? '#b91c1c' : '#9a3412', fontWeight: 600, fontSize: '0.7rem' }}
                    />
                  </TableCell>
                </TableRow>
              );
            })}
          </AlertPanel>
        </Grid>
      </Grid>

      {/* Activity Log Table */}
      <Paper sx={{ width: '100%', borderRadius: 2, boxShadow: '0 1px 3px 0 rgba(0,0,0,0.1)', overflow: 'hidden' }}>
        <Box sx={{ p: 2, borderBottom: '1px solid #e5e7eb', backgroundColor: '#f8fafc' }}>
          <Typography variant="h6" sx={{ fontWeight: 600, color: '#1f2937' }}>{t('recentConsumptionLogs')}</Typography>
        </Box>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell sx={{ fontWeight: 600 }}>{t('time')}</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>{t('action')}</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>{t('barcode')}</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>{t('chemical')}</TableCell>
              <TableCell sx={{ fontWeight: 600 }}>{t('operator')}</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {stats.recentActivity.length === 0 ? (
              <TableRow>
                <TableCell colSpan={5} align="center" sx={{ py: 8 }}>
                  <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', opacity: 0.8 }}>
                    <img src="/empty-state.jpg" alt="Empty state" style={{ width: '200px', borderRadius: '16px', marginBottom: '16px', boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.1)' }} />
                    <Typography variant="subtitle1" sx={{ fontWeight: 600, color: '#334155' }}>{t('noTransactionsYet')}</Typography>
                    <Typography variant="body2" sx={{ color: '#64748b', maxWidth: 300, textAlign: 'center', mt: 1 }}>
                      {t('noTransactionsDesc')}
                    </Typography>
                  </Box>
                </TableCell>
              </TableRow>
            ) : (
              stats.recentActivity.map((activity, idx) => (
                <TableRow key={idx} hover>
                  <TableCell sx={{ color: '#6b7280', fontSize: '0.85rem' }}>
                    {new Date(activity.time).toLocaleString(language === 'tr' ? 'tr-TR' : 'en-US')}
                  </TableCell>
                  <TableCell>
                    <Chip label={t('consumed')} size="small" sx={{ backgroundColor: '#dcfce7', color: '#166534', fontWeight: 600, fontSize: '0.7rem' }} />
                  </TableCell>
                  <TableCell sx={{ fontFamily: 'monospace', fontWeight: 600 }}>{activity.barcode}</TableCell>
                  <TableCell>{activity.chemical}</TableCell>
                  <TableCell sx={{ fontWeight: 500 }}>{activity.operatorName || t('unknown')}</TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </Paper>
    </Box>
  );
});

export default Dashboard;
