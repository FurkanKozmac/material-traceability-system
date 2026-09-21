import os

pages = {
    "AddressManagement": "Address Management",
    "ShopManagement": "Shop Management",
    "StockMonitoring": "Stock Monitoring",
    "HmsReceive": "HMS Receive",
    "ReLifeDashboard": "Re-Life Requests",
    "HmsPick": "HMS Pick",
    "BarrelPicking": "Barrel Picking",
    "TankerLoad": "Tanker Load",
    "VehicleTrace": "Vehicle Trace"
}

template = """import React from 'react';
import {{ Box, Typography, Paper }} from '@mui/material';

export default function {name}() {{
  return (
    <Box>
      <Typography variant="h4" gutterBottom>{title}</Typography>
      <Paper sx={{ p: 3 }}>
        <Typography>This module is under construction.</Typography>
      </Paper>
    </Box>
  );
}}
"""

base_dir = "/Users/furkan/Desktop/mts/web-portal/src/pages"

for name, title in pages.items():
    path = os.path.join(base_dir, f"{name}.jsx")
    with open(path, "w") as f:
        f.write(template.format(name=name, title=title))
