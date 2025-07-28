// index.tsx
import { BrowserRouter, Routes, Route } from "react-router-dom";
import Layout from "../layout/Layout";
import Dashboard from "../pages/Dashboard";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Layout />}>
          <Route index element={<Dashboard />} />
          {/* سایر صفحات */}
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
