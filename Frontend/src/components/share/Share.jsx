import "./share.css";
import React, { useState } from 'react';
import {PermMedia, Label,Room, EmojiEmotions} from "@mui/icons-material"
import axios from "axios";
import Loader from "../loader/Loader";

export default function Share() {

  const [description, setDescription] = useState('');
  const userId = sessionStorage.getItem('Id')
  const [imageFile, setImageFile] = useState(null);
  const [isLoading, setIsLoading] = useState(false)
  const image = sessionStorage.getItem('url')

    function Share(){

          const formData = new FormData();
    formData.append("description", description);
    formData.append("UserId", userId);
    formData.append("ImageFile", imageFile);
  
    setIsLoading(true)
    
    setTimeout(()=>{
      axios.post("https://localhost:7151/api/Post", formData, {
        headers: {
          'Content-Type': 'multipart/form-data'
        }
        }).then(response => {
          setIsLoading(false)
      }).catch(error => {
          // Handle errors
          window.location.reload()
          setIsLoading(false)
          console.error(error);
      });
    },5000)
    }
  
  return (
    <div className="share">
      <div className="shareWrapper">
      {isLoading 
       && <Loader/>}


        <div className="shareTop">
          <img className="shareProfileImg" src={image} alt=""   />
          <input
            placeholder="Whats in your mind? "
            className="shareInput"
            type="text"
            value={description}
            onChange={(e)=>{
              setDescription(e.target.value);
            }}
          />
        </div>
        <hr className="shareHr"/>
        <div className="shareBottom">
            <div className="shareOptions">
                <div className="shareOption">
                    <PermMedia htmlColor="tomato" className="shareIcon"/>
                    <label className="shareOptionText shareFileLabel" htmlFor="shareMedia">
                    <input type="file" id="shareMedia" className="shareFileInput"  onChange={(e) => {
                  setImageFile(e.target.files[0])
              }}/>
              {imageFile ? imageFile.name : 'Photo or Video'}
              </label>
                </div>
                <div className="shareOption">
                    <Label htmlColor="blue" className="shareIcon"/>
                    <span className="shareOptionText">Tag</span>
                </div>
                <div className="shareOption">
                    <Room htmlColor="green" className="shareIcon"/>
                    <span className="shareOptionText">Location</span>
                </div>
                <div className="shareOption">
                    <EmojiEmotions htmlColor="goldenrod" className="shareIcon"/>
                    <span className="shareOptionText">Feelings</span>
                </div>
            </div>
            <button className="shareButton" type="button" onClick={Share}>Share</button>
        </div>
      </div>
    </div>
  );
}
