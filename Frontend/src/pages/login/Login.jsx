import axios from "axios";
import { useState } from "react";
import Button from 'react-bootstrap/Button';
import Form from 'react-bootstrap/Form';
import Modal from 'react-bootstrap/Modal';
import {useNavigate } from "react-router-dom";
import { isExpired, decodeToken } from "react-jwt";
import Loader from "../../components/loader/Loader";
import AppLogo from "../../components/logo/AppLogo";
import "./login.css";

const Login = () => {

  const navigate = useNavigate();

  const [show, setShow] = useState(sessionStorage.getItem("isLoggedIn")!=='true');
  const [isLoading, setIsLoading] = useState(false)
  const handleClose = () =>{
    if(!email || !password){
      return false;
    }
    setShow(false);
  };


    const [email,setEmail] = useState("");
    const [password,setPassword] = useState("");


  
function loginUser(){

  const formData = new FormData();
  formData.append("email", email);
  formData.append("password", password); 



  setTimeout(() =>{
    setIsLoading(true);
    axios.post("https://localhost:7151/api/Account/login",formData)
    .then((response)=>{
      console.log(response);
      setIsLoading(false)
        sessionStorage.setItem("isLoggedIn", 'true');
        sessionStorage.setItem("tokens",response.data.token);
        sessionStorage.setItem("Id",response.data.id);
        sessionStorage.setItem("url",response.data.imagePath);
        const token = sessionStorage.getItem("tokens");    
        const decoded = decodeToken(token);
          const isMyTokenExpired = isExpired(token);
        console.log(decoded);
        console.log(isMyTokenExpired);

        const claimValue = decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
        if(claimValue==='Admin'){
           navigate('/')
         }
         const claimName= decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name']
 
         
         console.log(claimName);
         console.log(claimValue);
 
         sessionStorage.setItem('name',claimName);
         sessionStorage.setItem('role', claimValue);
         handleClose();
         window.location.reload();

  },5000)
   
    });

  }
return(

  <div>
<Modal show={show} onHide={handleClose} centered dialogClassName="social-login-dialog">
        <div className="socialLoginShell">
          <div className="socialLoginBrand">
            <AppLogo light showTagline />
            <div className="socialLoginBrandCopy">
              <span>Community hub</span>
              <h2>Share your moments with the people who matter.</h2>
              <p>Sign in to post updates, follow friends, and keep your conversations moving.</p>
            </div>
            <div className="socialLoginStats">
              <div>
                <strong>24k</strong>
                <span>connections</span>
              </div>
              <div>
                <strong>8.7k</strong>
                <span>daily posts</span>
              </div>
            </div>
          </div>
          <div className="socialLoginPanel">
            <button className="socialLoginClose" type="button" onClick={handleClose} aria-label="Close login">
              &times;
            </button>
            <div className="socialLoginHeader">
              <AppLogo compact />
              <h1>Welcome back</h1>
              <p>Enter your details to continue to SocialButterfly.</p>
            </div>
          <Form>
            <Form.Group className="mb-3" controlId="exampleForm.ControlInput1">
              <Form.Label>Email</Form.Label>
              <div className="socialInputWrap">
                <span className="socialInputIcon">@</span>
              <Form.Control
                type="text"
                placeholder="you@example.com"
                value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
              }}
                autoFocus
              />
              </div>
            </Form.Group>
            <Form.Group className="mb-1" controlId="exampleForm.ControlInput2">
              <Form.Label>Password</Form.Label>
              <div className="socialInputWrap">
                <span className="socialInputIcon">*</span>
              <Form.Control
                type="password"
                placeholder="Enter password"
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value)
              }}

              />
              </div>
            </Form.Group>
          </Form>
          <div className="socialLoginMeta">
            <label>
              <input type="checkbox" />
              Remember me
            </label>
            <button type="button">Forgot password?</button>
          </div>
          <Button className="socialLoginSubmit" variant="primary" onClick={loginUser}>
            Sign in
          </Button>
          <Button variant="link" className="socialLoginCreate" onClick={()=>navigate('/register')}>
             Create a new account
          </Button>
          </div>
        </div>
      </Modal>
      {isLoading &&
        <Loader/>}
</div>
);
}

export default Login;
