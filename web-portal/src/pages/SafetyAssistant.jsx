import React, { useEffect, useState } from 'react';
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { AlertTriangle, ArrowUpRight, Flame, LockKeyhole, ShieldCheck, Sparkles, Thermometer, Droplets } from 'lucide-react';
import api from '../api';
import { useLanguage } from '../useLanguage';

const quickActions = [
  { key: 'fire', icon: <Flame size={17} />, tone: '#b42318' },
  { key: 'firstAid', icon: <Droplets size={17} />, tone: '#1769aa' },
  { key: 'spill', icon: <AlertTriangle size={17} />, tone: '#a15c00' },
  { key: 'storage', icon: <Thermometer size={17} />, tone: '#147d64' },
];

function getErrorMessage(error, fallback) {
  const data = error.response?.data;
  return data?.message || data?.title || (typeof data === 'string' ? data : fallback);
}

export default function SafetyAssistant() {
  const { t } = useLanguage();
  const safety = (key) => t(`safetyAssistant.${key}`);
  const [chemicals, setChemicals] = useState([]);
  const [chemicalCode, setChemicalCode] = useState('CC-202');
  const [question, setQuestion] = useState('');
  const [answer, setAnswer] = useState(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [chemicalLoadError, setChemicalLoadError] = useState('');

  useEffect(() => {
    let active = true;
    api.get('/chemicals')
      .then((response) => {
        if (!active) return;
        const loadedChemicals = response.data || [];
        setChemicals(loadedChemicals);
        if (loadedChemicals.some((chemical) => chemical.chemicalCode === 'CC-202')) setChemicalCode('CC-202');
        else if (loadedChemicals[0]?.chemicalCode) setChemicalCode(loadedChemicals[0].chemicalCode);
      })
      .catch((requestError) => {
        if (active) setChemicalLoadError(getErrorMessage(requestError, safety('chemicalLoadFailed')));
      });
    return () => { active = false; };
  }, [t]);

  const askQuestion = async (selectedQuestion = question) => {
    const trimmedQuestion = selectedQuestion.trim();
    if (!trimmedQuestion) {
      setError(safety('emptyQuestion'));
      return;
    }
    setQuestion(trimmedQuestion);
    setLoading(true);
    setError('');
    setAnswer(null);
    try {
      const response = await api.post('/safetyassistant/ask', { chemicalCode, question: trimmedQuestion });
      setAnswer(response.data);
    } catch (requestError) {
      setError(getErrorMessage(requestError, safety('answerFailed')));
    } finally {
      setLoading(false);
    }
  };

  const handleSubmit = (event) => {
    event.preventDefault();
    askQuestion();
  };

  return (
    <Box sx={{ maxWidth: 1280, mx: 'auto', width: '100%', pb: 5 }}>
      <Paper sx={{ p: { xs: 2.5, md: 4 }, mb: 3, borderRadius: 3, color: '#fff', overflow: 'hidden', position: 'relative', background: 'linear-gradient(125deg, #102a43 0%, #145374 62%, #197278 100%)', boxShadow: '0 16px 32px rgba(15, 42, 67, 0.18)' }}>
        <Box sx={{ position: 'absolute', right: { xs: -70, md: 30 }, top: -80, width: 240, height: 240, border: '1px solid rgba(255,255,255,0.14)', borderRadius: '50%' }} />
        <Stack direction="row" spacing={2} alignItems="flex-start" sx={{ position: 'relative' }}>
          <Box sx={{ p: 1.5, borderRadius: 2.5, backgroundColor: 'rgba(255,255,255,0.13)', display: 'flex' }}><ShieldCheck size={30} /></Box>
          <Box>
            <Typography variant="h4" sx={{ fontWeight: 750, letterSpacing: '-0.02em', fontSize: { xs: '1.65rem', md: '2.1rem' } }}>🛡️ {safety('title')} (RAG)</Typography>
            <Typography sx={{ mt: 1, color: '#d8edf2', maxWidth: 680 }}>{safety('subtitle')}</Typography>
          </Box>
        </Stack>
      </Paper>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', lg: 'minmax(280px, 0.75fr) minmax(0, 1.6fr)' }, gap: 3 }}>
        <Stack spacing={3}>
          <Paper sx={{ p: 3, borderRadius: 2.5, border: '1px solid #e5e7eb', boxShadow: '0 4px 12px rgba(15, 23, 42, 0.04)' }}>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}><LockKeyhole size={18} color="#2563eb" /><Typography variant="h6" sx={{ fontWeight: 700, color: '#172b4d' }}>{safety('scopeTitle')}</Typography></Stack>
            <FormControl fullWidth size="small">
              <InputLabel id="chemical-select-label">{safety('selectChemical')}</InputLabel>
              <Select labelId="chemical-select-label" value={chemicalCode} label={safety('selectChemical')} onChange={(event) => setChemicalCode(event.target.value)}>
                {chemicals.length === 0 && <MenuItem value="CC-202">CC-202 - Clear Coat</MenuItem>}
                {chemicals.map((chemical) => <MenuItem key={chemical.id || chemical.chemicalCode} value={chemical.chemicalCode}>{chemical.chemicalCode} - {chemical.name}</MenuItem>)}
              </Select>
            </FormControl>
            {chemicalLoadError && <Alert severity="warning" sx={{ mt: 2 }}>{chemicalLoadError} {safety('fallbackChemicalNote')}</Alert>}
            <Typography variant="caption" sx={{ display: 'block', mt: 2, color: '#64748b', lineHeight: 1.5 }}>{safety('scopeNote')}</Typography>
          </Paper>

          <Paper sx={{ p: 3, borderRadius: 2.5, backgroundColor: '#fffaf0', border: '1px solid #f5d9a6' }}>
            <Typography variant="subtitle1" sx={{ fontWeight: 750, color: '#68420c', mb: 0.5 }}>{safety('quickTitle')}</Typography>
            <Typography variant="body2" sx={{ color: '#8a641f', mb: 2 }}>{safety('quickDescription')}</Typography>
            <Stack spacing={1}>
              {quickActions.map((action) => (
                <Button key={action.key} variant="outlined" onClick={() => askQuestion(safety(`quickChips.${action.key}`))} startIcon={action.icon} endIcon={<ArrowUpRight size={15} />} sx={{ justifyContent: 'space-between', textAlign: 'left', color: action.tone, borderColor: '#edcf98', textTransform: 'none', lineHeight: 1.3, py: 1.05, '&:hover': { borderColor: action.tone, backgroundColor: 'rgba(255,255,255,0.65)' } }}>
                  {safety(`quickChips.${action.key}`)}
                </Button>
              ))}
            </Stack>
          </Paper>
        </Stack>

        <Stack spacing={3}>
          <Paper component="form" onSubmit={handleSubmit} sx={{ p: { xs: 2.5, md: 3 }, borderRadius: 2.5, border: '1px solid #dbe5ef', boxShadow: '0 4px 12px rgba(15, 23, 42, 0.05)' }}>
            <Stack direction="row" spacing={1} alignItems="center" sx={{ mb: 2 }}><Sparkles size={19} color="#147d64" /><Typography variant="h6" sx={{ fontWeight: 700, color: '#172b4d' }}>{safety('askTitle')}</Typography></Stack>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <TextField fullWidth multiline minRows={2} maxRows={5} label={safety('askPlaceholder')} placeholder={safety('askPlaceholder')} value={question} onChange={(event) => setQuestion(event.target.value)} disabled={loading} />
              <Button type="submit" variant="contained" disabled={loading || !question.trim()} sx={{ minWidth: { sm: 132 }, minHeight: { xs: 48, sm: 'auto' }, textTransform: 'none', fontWeight: 700, backgroundColor: '#147d64', '&:hover': { backgroundColor: '#0f624f' } }}>{loading ? <CircularProgress size={22} color="inherit" /> : safety('askButton')}</Button>
            </Stack>
            {loading && <Stack direction="row" spacing={1} alignItems="center" sx={{ mt: 2, color: '#147d64' }}><CircularProgress size={16} color="inherit" /><Typography variant="body2">{safety('loadingText')}</Typography></Stack>}
          </Paper>

          {error && <Alert severity="error" onClose={() => setError('')}>{error}</Alert>}
          {answer && <Paper sx={{ p: { xs: 2.5, md: 3.5 }, borderRadius: 2.5, border: '1px solid #b8dfd2', background: 'linear-gradient(145deg, #f4fffb 0%, #ffffff 70%)', boxShadow: '0 8px 18px rgba(20, 125, 100, 0.08)' }}>
            <Stack direction="row" justifyContent="space-between" alignItems="flex-start" gap={2} sx={{ mb: 2 }}><Box><Typography variant="overline" sx={{ color: '#147d64', fontWeight: 800, letterSpacing: '0.1em' }}>{safety('assistantResponse')}</Typography><Typography variant="body2" sx={{ color: '#64748b', mt: 0.25 }}>{answer.question}</Typography></Box><Chip label={answer.success ? safety('verifiedBadge') : safety('reviewBadge')} size="small" color={answer.success ? 'success' : 'warning'} /></Stack>
            <Typography sx={{ whiteSpace: 'pre-wrap', color: '#1f2937', lineHeight: 1.75, fontSize: '1rem' }}>{answer.answer}</Typography>
            <Chip label={`${safety('sourcePrefix')} ${answer.relevantSectionTitle}`} size="small" sx={{ mt: 3, fontWeight: 700, color: '#0f624f', backgroundColor: '#d9f4eb' }} />
          </Paper>}
          {!answer && !loading && !error && <Paper sx={{ p: 4, borderRadius: 2.5, border: '1px dashed #cbd5e1', backgroundColor: '#f8fafc', textAlign: 'center' }}><ShieldCheck size={34} color="#94a3b8" /><Typography sx={{ mt: 1.5, color: '#475569', fontWeight: 650 }}>{safety('readyTitle')}</Typography><Typography variant="body2" sx={{ mt: 0.5, color: '#94a3b8' }}>{safety('readyDescription')}</Typography></Paper>}
        </Stack>
      </Box>
    </Box>
  );
}
