import { AppBar, Toolbar, Typography, Box, IconButton, Menu, MenuItem, Avatar } from '@mui/material';
import NotificationsNoneIcon from '@mui/icons-material/NotificationsNone';
import AccountCircle from '@mui/icons-material/AccountCircle';
import { useState } from 'react';

const headerHeight = 64; // مطمئن شو که با Layout یکی باشه

export default function Header() {
  const [anchorEl, setAnchorEl] = useState<null | HTMLElement>(null);
  const open = Boolean(anchorEl);

  const handleProfileClick = (event: React.MouseEvent<HTMLElement>) => {
    setAnchorEl(event.currentTarget);
  };

  const handleClose = () => setAnchorEl(null);

  return (
    <AppBar position="fixed" color="default" elevation={1} sx={{ height: headerHeight }}>
      <Toolbar sx={{ justifyContent: 'space-between', direction: 'rtl', height: '100%' }}>
        <Typography variant="h6" sx={{ order: 2 }}>
          پنل مدیریت
        </Typography>

        <Box
          sx={{
            display: 'flex',
            alignItems: 'center',
            gap: 1,
            order: 1,
            direction: 'ltr',
          }}
        >
          <IconButton color="inherit">
            <NotificationsNoneIcon />
          </IconButton>

          <IconButton color="inherit" onClick={handleProfileClick}>
            <Avatar>
              <AccountCircle />
            </Avatar>
          </IconButton>

          <Menu anchorEl={anchorEl} open={open} onClose={handleClose}>
            <MenuItem onClick={handleClose}>پروفایل</MenuItem>
            <MenuItem onClick={handleClose}>خروج</MenuItem>
          </Menu>
        </Box>
      </Toolbar>
    </AppBar>
  );
}
