import React from 'react';
import { Box, Typography, Button } from '@mui/material';
import { useLanguage } from '../useLanguage';

class ErrorBoundaryView extends React.Component {
  constructor(props) {
    super(props);
    this.state = { hasError: false, error: null };
  }

  static getDerivedStateFromError(error) {
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    console.error("ErrorBoundary caught an error", error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return (
        <Box sx={{ p: 4, textAlign: 'center', mt: 10 }}>
          <Typography variant="h4" color="error" gutterBottom>
            {this.props.t('unexpectedError')}
          </Typography>
          <Typography variant="body1" color="text.secondary" sx={{ mb: 3 }}>
            {this.state.error?.toString()}
          </Typography>
          <Button 
            variant="contained" 
            onClick={() => window.location.reload()}
          >
            {this.props.t('reloadPage')}
          </Button>
        </Box>
      );
    }

    return this.props.children; 
  }
}

export default function ErrorBoundary(props) {
  const { t } = useLanguage();
  return <ErrorBoundaryView {...props} t={t} />;
}
