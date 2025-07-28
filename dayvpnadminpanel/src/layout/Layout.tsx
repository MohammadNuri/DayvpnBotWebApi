import { Box } from '@mui/material';
import Header from './Header';
import Sidebar from './Sidebar';
import { Outlet } from 'react-router-dom';

const drawerWidth = 240;
const headerHeight = 64;

export default function Layout() {
  return (
    <>
      {/* هدر همیشه بالاست */}
      <Header />

      {/* سایدبار ثابت سمت راست، زیر هدر */}
      <Sidebar
        sx={{
          position: 'fixed',
          top: headerHeight,
          right: 0,
          width: drawerWidth,
          height: `calc(100vh - ${headerHeight}px)`,
        }}
      />

      {/* محتوای اصلی */}
      <Box
        component="main"
        sx={{
          marginTop: `${headerHeight}px`,
          marginInlineEnd: `${drawerWidth}px`, // اینجا marginRight و marginLeft به صورت هوشمند
          padding: 2,
          minHeight: `calc(100vh - ${headerHeight}px)`,
          bgcolor: 'background.default',
          overflowY: 'auto',
          direction: 'rtl',     // جهت محتوای اصلی
          textAlign: 'left',
        }}
      >
        <Outlet />
      </Box>
    </>
  );
}
