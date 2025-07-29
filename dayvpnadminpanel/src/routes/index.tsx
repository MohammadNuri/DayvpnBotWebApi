// index.tsx
import { BrowserRouter, Routes, Route } from "react-router-dom";
import Dashboard from "../layout/Dashboard";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Dashboard />}>
          {/* سایر صفحات */}
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
