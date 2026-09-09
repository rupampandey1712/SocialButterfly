import React, { useState } from 'react';
import { useNavigate,Link } from "react-router-dom";
import 'react-datepicker/dist/react-datepicker.css';
import axios from 'axios';
import './register.css';


function Register(){

  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const role="User";
  const [dob, setDob] = useState("");
  const [imageFile, setImageFile] = useState(null);
  const [imagePreview, setImagePreview] = useState(null);
  const [information, SetInformation] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const navigate = useNavigate();

  const handleImageChange = (e) => {
    const file = e.target.files && e.target.files[0];
    if (file) {
      setImageFile(file);
      setImagePreview(URL.createObjectURL(file));
    } else {
      setImageFile(null);
      setImagePreview(null);
    }
  };

  async function registerUser(e) {
    if (e && e.preventDefault) e.preventDefault();

    if (!name || !email || !password) {
      alert("Please fill in Name, Email, and Password.");
      return;
    }

    setIsSubmitting(true);
    const formattedDate = dob ? new Date(dob).toISOString() : new Date().toISOString();

    const formData = new FormData();
    formData.append("Name", name);
    formData.append("Email", email);
    formData.append("Password", password);
    formData.append("Dob", formattedDate);
    formData.append("Role", role);
    if (imageFile) {
      formData.append("ImageFile", imageFile);
    }
    formData.append("Information", information || "");

    try {
      await axios.post("https://localhost:7151/api/Account/register", formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
      });
      alert('Registration successful! Please sign in.');
      navigate('/');
    } catch (error) {
      console.error("Registration failed:", error);
      const errorMsg = error.response?.data || error.message || "Registration failed. Please try again.";
      alert(typeof errorMsg === 'string' ? errorMsg : JSON.stringify(errorMsg));
    } finally {
      setIsSubmitting(false);
    }
  } 
  
  
    return (


      <section className="auth-page">
  <div className="container h-100">
    <div className="row d-flex justify-content-center align-items-center h-100">
      <div className="col-lg-12 col-xl-11">
        <div className="card text-black auth-card">
          <div className="card-body p-md-5">
            <div className="row justify-content-center">
              <div className="col-md-10 col-lg-6 col-xl-5 order-2 order-lg-1">

                <p className="text-center h1 fw-bold mb-2 mx-1 mx-md-4 mt-4">Create account</p>
                <p className="auth-subtitle text-center mb-5">Join Social Butterfly and start sharing with your circle.</p>

                <form className="mx-1 mx-md-4">

                  <div className="d-flex flex-row align-items-center mb-4">
                    <i className="fas fa-user fa-lg me-3 fa-fw"></i>
                    <div className="form-outline flex-fill mb-0">
                      <input type="text" id="form3Example1c" className="form-control" value={name}
                onChange={(e) => {
                  setName(e.target.value)
              }} placeholder='Your Name'/>
                    </div>
                  </div>

                  <div className="d-flex flex-row align-items-center mb-4">
                    <i className="fas fa-calendar fa-lg me-3 fa-fw"></i>
                    <div className="form-outline flex-fill mb-0">
                
                   <input type="date" id="form3Example2c" className="form-control" value={dob}
                onChange={(e) => {
                  setDob(e.target.value)
              }} placeholder='Your Dob'/> 
                    </div>
                  </div>

                  <div className="d-flex flex-row align-items-center mb-4">
                    <i className="fas fa-envelope fa-lg me-3 fa-fw"></i>
                    <div className="form-outline flex-fill mb-0">
                      <input type="email" id="form3Example3c" className="form-control" value={email}
                onChange={(e) => {
                  setEmail(e.target.value)
              }} placeholder='Your Email'/>
                    </div>
                  </div>

                  <div className="d-flex flex-row align-items-center mb-4">
                    <i className="fas fa-lock fa-lg me-3 fa-fw"></i>
                    <div className="form-outline flex-fill mb-0">
                      <input type="password" id="form3Example4c" className="form-control" value={password}
                onChange={(e) => {
                  setPassword(e.target.value)
              }} placeholder='Password'/>
                    </div>
                  </div>

                  <div className="d-flex flex-row align-items-center mb-4">
                    <i className="fas fa-image fa-lg me-3 fa-fw"></i>
                    <div className="form-outline flex-fill mb-0">
                      <label htmlFor="form3Example4cd" className="form-label text-muted small mb-1">Profile Picture</label>
                      <input
                        type="file"
                        id="form3Example4cd"
                        className="form-control"
                        accept="image/*"
                        onChange={handleImageChange}
                      />
                    </div>
                  </div>

                  {imagePreview && (
                    <div className="d-flex justify-content-center mb-4">
                      <div className="text-center">
                        <img
                          src={imagePreview}
                          alt="Profile Preview"
                          style={{
                            width: "90px",
                            height: "90px",
                            objectFit: "cover",
                            borderRadius: "50%",
                            border: "3px solid #1877f2",
                            boxShadow: "0 2px 8px rgba(0,0,0,0.15)"
                          }}
                        />
                        <div className="small text-muted mt-1">Profile Preview</div>
                      </div>
                    </div>
                  )}

                  <div className="d-flex flex-row align-items-center mb-4">
                    <i className="fas fa-info-circle fa-lg me-3 fa-fw"></i>
                    <div className="form-outline flex-fill mb-0">
                      <input type="text" id="form3Example5cd" className="form-control" value={information}  onChange={(e) => {
                  SetInformation(e.target.value)
              }} placeholder="Bio / Information"/>
                    </div>
                  </div>

                  <div className="form-check d-flex justify-content-center mb-5">
                    <input className="form-check-input me-2" type="checkbox" value="" id="form2Example3c" />
                    <label className="form-check-label" htmlFor="form2Example3">
                      I agree all statements in <a href="#!">Terms of service</a>
                    </label>
                  </div>

                  <div className="d-flex justify-content-center mx-4 mb-3 mb-lg-4">
                    <button
                      type="button"
                      className="btn btn-primary btn-lg"
                      onClick={registerUser}
                      disabled={isSubmitting}
                    >
                      {isSubmitting ? "Creating account..." : "Register"}
                    </button>
                  </div>
                
                  <p>
                       Already registered <Link to='/'>Login</Link>
                   </p>
    

                </form>

              </div>
              <div className="col-md-10 col-lg-6 col-xl-7 d-flex align-items-center order-1 order-lg-2 auth-visual">

                <img src="https://mdbcdn.b-cdn.net/img/Photos/new-templates/bootstrap-registration/draw1.webp"
                  className="img-fluid" alt=''/>

              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</section>
       
      );
    }
   
export default Register;
