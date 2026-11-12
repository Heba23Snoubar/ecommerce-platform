import { Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import AppLayout from "./components/AppLayout";
import ProtectedRoute from "./components/ProtectedRoute";
import LoginPage from "./pages/LoginPage";
import RegisterPage from "./pages/RegisterPage";
import ProductsPage from "./pages/ProductsPage";
import ProductDetailsPage from "./pages/ProductDetailsPage";
import AddProductPage from "./pages/AddProductPage";
import CheckoutPage from "./pages/CheckoutPage";
import OrdersPage from "./pages/OrdersPage";
import OrderDetailsPage from "./pages/OrderDetailsPage";
import NotificationsPage from "./pages/NotificationsPage";

function HomeRedirect(){const {user}=useAuth();return <Navigate to={user?"/products":"/login"} replace/>;}
export default function App(){
  return <Routes>
    <Route path="/" element={<HomeRedirect/>}/>
    <Route path="/login" element={<LoginPage/>}/>
    <Route path="/register" element={<RegisterPage/>}/>
    <Route element={<ProtectedRoute/>}>
      <Route element={<AppLayout/>}>
        <Route path="/products" element={<ProductsPage/>}/>
        <Route path="/products/:id" element={<ProductDetailsPage/>}/>
        <Route path="/orders" element={<OrdersPage/>}/>
        <Route path="/orders/:id" element={<OrderDetailsPage/>}/>
        <Route path="/notifications" element={<NotificationsPage/>}/>
        <Route element={<ProtectedRoute allowedRoles={["Customer"]}/>}>
          <Route path="/checkout" element={<CheckoutPage/>}/>
        </Route>
        <Route element={<ProtectedRoute allowedRoles={["Admin"]}/>}>
          <Route path="/products/new" element={<AddProductPage/>}/>
        </Route>
      </Route>
    </Route>
    <Route path="*" element={<HomeRedirect/>}/>
  </Routes>;
}
