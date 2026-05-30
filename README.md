# SocialMediaWeb

SocialMediaWeb is a full-stack social media web application built with an ASP.NET Core Web API backend and a React frontend. It supports user authentication, social posts, comments, likes, friend requests, profile management, and an admin dashboard.

## Features

- User registration and login
- JWT-based authentication and authorization
- User profiles and profile media
- Social media posts with image/file upload support
- Comments and likes
- Friend request management
- Admin dashboard with pages for dashboard, settings, tables, maps, and calendar
- Swagger UI for API testing in development
- SQL Server database integration with Entity Framework Core

## Tech Stack

### Backend

- ASP.NET Core Web API
- .NET 10.0
- Entity Framework Core
- SQL Server
- JWT Bearer Authentication
- Swagger / Swashbuckle
- BCrypt password hashing

### Frontend

- React 18
- React Router
- Axios
- Bootstrap / React Bootstrap
- Material UI
- Tailwind CSS
- Chart.js

## Project Structure

```text
SocialMediaWeb/
+-- Backend/
|   +-- Controllers/
|   +-- Dtos/
|   +-- Migrations/
|   +-- Models/
|   +-- Services/
|   +-- wwwroot/
|   +-- Program.cs
|   +-- SocialMediaWeb.csproj
|   +-- appsettings.json
+-- Frontend/
|   +-- public/
|   +-- src/
|   +-- package.json
|   +-- tailwind.config.js
+-- README.md
```

## Prerequisites

Install the following before running the project:

- .NET SDK compatible with the backend target framework
- SQL Server or SQL Server Express
- Node.js and npm
- Visual Studio, Visual Studio Code, or another preferred editor

## Backend Setup

1. Open the backend folder:

   ```bash
   cd Backend
   ```

2. Update the SQL Server connection string in `appsettings.json`:

   ```json
   "ConnectionStrings": {
     "Con": "Data Source=YOUR_SERVER;Initial Catalog=SocialMedia;Integrated Security=True;Encrypt=false;"
   }
   ```

3. Restore backend dependencies:

   ```bash
   dotnet restore
   ```

4. Apply Entity Framework migrations:

   ```bash
   dotnet ef database update
   ```

5. Run the API:

   ```bash
   dotnet run
   ```

6. In development, open Swagger UI from the URL shown in the terminal, usually:

   ```text
   https://localhost:5001/swagger
   ```

## Frontend Setup

1. Open the frontend folder:

   ```bash
   cd Frontend
   ```

2. Install dependencies:

   ```bash
   npm install
   ```

3. Start the React app:

   ```bash
   npm start
   ```

4. Open the app in the browser:

   ```text
   http://localhost:3000
   ```

## Available Frontend Scripts

Run these commands inside the `Frontend` folder:

```bash
npm start
npm run build
npm test
```

## Environment Configuration

The backend uses the following important configuration sections in `appsettings.json`:

- `ConnectionStrings:Con` for the SQL Server database connection
- `JWT:ValidAudience` for the allowed JWT audience
- `JWT:ValidIssuer` for the token issuer
- `JWT:Secret` for signing JWT tokens
- `JWT:TokenValidityInMinutes` for access token lifetime
- `JWT:RefreshTokenValidityInDays` for refresh token lifetime

For production, move sensitive values such as database credentials and JWT secrets to environment variables, user secrets, or a secure secret manager.

## API Notes

- Swagger is enabled in development mode.
- Static files are served from `Backend/wwwroot`.
- Uploaded images are stored under `wwwroot/Images`.
- Uploaded post files are stored under `wwwroot/posts`.
- CORS is configured to allow the React frontend to call the API.

## Build

Build the backend:

```bash
cd Backend
dotnet build
```

Build the frontend:

```bash
cd Frontend
npm run build
```

## Security Notes

- Do not commit production database credentials.
- Do not use development JWT secrets in production.
- Use HTTPS in deployed environments.
- Review CORS settings before deploying publicly.

## License

This project is available for educational and development use. Add a license file before publishing it as an open-source repository.
