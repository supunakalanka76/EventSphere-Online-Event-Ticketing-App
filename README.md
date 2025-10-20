# 🎟️ EventSphere – Online Event Management & Ticketing System

EventSphere is a full-stack **ASP.NET MVC web application** designed to simplify event organization, ticket booking, and digital ticket verification using QR codes.  
It provides a secure, user-friendly, and scalable platform for **event organizers, customers, and administrators**.

---

## 🚀 Features

### 👥 User Roles
- **Admin** – Manage users, events, venues, and system reports  
- **Organizer** – Create, edit, and monitor events and ticket sales  
- **Customer** – Browse events, book tickets, and download QR-enabled e-tickets  

### 💡 Core Functionalities
- User authentication & role-based access control  
- Event listing and management  
- Online booking and payment system  
- QR code generation for each booking  
- Real-time reporting and analytics  
- Loyalty point tracking and rewards  
- PDF & Excel export for reports  

---

## 🏗️ System Architecture

EventSphere follows the **3-tier architecture** and **MVC (Model–View–Controller)** pattern for clear separation of concerns:

1. **Presentation Layer** – Razor views and controllers  
2. **Business Logic Layer** – Models, services, and validation  
3. **Data Access Layer** – Entity Framework (EF) connected to SQL Server  

---

## 🧱 Database Design

Database Name: **EventSphereDB**

### Key Tables
- `Users` – User profiles, credentials, and roles  
- `Events` – Event details with organizer references  
- `Venues` – Event location data  
- `Bookings` – User reservations and seat management  
- `Payments` – Transaction history  
- `Tickets` – QR-based ticket information  
- `LoyaltyTransactions` – Reward points tracking  

Entity Framework handles object-relational mapping between the database and application classes.

---

## ⚙️ Installation and Configuration

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/)  
- [.NET Framework 6.0 or later](https://dotnet.microsoft.com/)  
- [SQL Server / SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-downloads)  
- [SSMS (SQL Server Management Studio)](https://aka.ms/ssmsfullsetup)

---

### 1️⃣ Clone the Repository
```bash
git clone https://github.com/supunakalanka76/EventSphere-Online-Event-Ticketing-App.git
```
### 2️⃣ Configure the Database
- Open SSMS
- Create a new database named EventSphereDB
- Execute the provided SQL script in /schema.sql

### 3️⃣ Update Connection String
- In Web.config (Replace YOUR_SERVER_NAME with your local SQL instance name.)
  ```
  <connectionStrings>
    <add name="EventSphereDBEntities" 
         connectionString="metadata=res://*/Models.EventSphereModel.csdl|res://*/Models.EventSphereModel.ssdl|res://*/Models.EventSphereModel.msl;provider=System.Data.SqlClient;provider connectionstring=&quot;data source=YOUR_SERVER_NAME\SQLEXPRESS;initial catalog=EventSphereDB;integrated security=True;trustservercertificate=True;MultipleActiveResultSets=True;App=EntityFramework&quot;" 
         providerName="System.Data.EntityClient" />
  </connectionStrings>
  ```
### 4️⃣ Run the Application
- Open the solution in Visual Studio
- Build the project (Ctrl + Shift + B)
- Run using IIS Express (F5)
- The app will open in your default browser at
  ```
  https://localhost:44319/
  ```
### 5️⃣ Default Admin Login
| Role  | Email                   | Password   |
| ----- | ----------------------- | ---------- |
| Admin | `admin@eventsphere.com` | `admin123` |

- Please change this password after first login for security.

---

## 🧩 Key Technologies Used
| Category        | Technology                          |
| --------------- | ----------------------------------- |
| Frontend        | HTML5, CSS3, Bootstrap, Razor Views |
| Backend         | ASP.NET MVC 6, C#                   |
| Database        | SQL Server                          |
| ORM             | Entity Framework                    |
| Reporting       | EPPlus (Excel), iTextSharp (PDF)    |
| Security        | ASP.NET Identity, Password Hashing  |
| QR Codes        | QRCoder Library                     |
| Version Control | Git / GitHub                        |

---

## 🙋‍♂️ Authors
#### Supun Akalanka
#### Final Year Application Development Project 02 – BEng (Hons) Software Engineering (TOP-UP)
#### London Metropolitan University / ESOFT Metro Campus
--
#### Sahan Lakmal
#### Final Year Application Development Project 02 – BEng (Hons) Software Engineering (TOP-UP)
#### London Metropolitan University / ESOFT Metro Campus

---
## 📜 License
#### This project is open-source and available under the MIT License.

### ⭐ If you found this useful, please give the repository a star!

<p align="right"> - Last Update: 20.10.2025 - </p>
