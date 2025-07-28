import { Drawer, List, ListItem, ListItemIcon, ListItemText, Toolbar } from '@mui/material';
import DashboardIcon from '@mui/icons-material/Dashboard';
import PeopleIcon from '@mui/icons-material/People';
import SubscriptionsIcon from '@mui/icons-material/SubscriptionsOutlined';
import { useNavigate } from 'react-router-dom';

const drawerWidth = 240;

export default function Sidebar(props: { sx?: any }) {
  const navigate = useNavigate();

  const menuItems = [
    { text: 'داشبورد', icon: <DashboardIcon />, path: '/' },
    { text: 'کاربران', icon: <PeopleIcon />, path: '/users' },
    { text: 'اشتراک‌ها', icon: <SubscriptionsIcon />, path: '/subscriptions' },
  ];

  return (
    <Drawer
      variant="permanent"
      anchor="left"
      sx={{
        width: drawerWidth,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: drawerWidth,
          boxSizing: 'border-box',
          backgroundColor: '#fff',
          borderLeft: '1px solid #ddd',
          direction: 'rtl',
          ...props.sx,
        },
      }}
      PaperProps={{ sx: props.sx }}
    >
      <Toolbar />
      <List>
        {menuItems.map(({ text, icon, path }) => (
          <ListItem
            component="button"
            key={text}
            onClick={() => navigate(path)}
            sx={{
              justifyContent: 'flex-start',
              color: 'rgba(0, 0, 0, 0.87)',
              '&:hover': { backgroundColor: 'rgba(0, 0, 0, 0.04)' },
              '& .MuiListItemIcon-root': {
                minWidth: 40,
                marginLeft: 'auto',
                marginRight: 0,
                color: 'rgba(0, 0, 0, 0.54)',
              },
            }}
          >
            <ListItemIcon>{icon}</ListItemIcon>
            <ListItemText primary={text} />
          </ListItem>
        ))}
      </List>
    </Drawer>
  );
}
