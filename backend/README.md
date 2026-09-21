# Material Tracking System (MTS) - Industrial Chemical WMS Core

An enterprise-grade, high-performance **Warehouse Management System (WMS)** backend built with **.NET 10**, **C# 13**, **Entity Framework Core**, and **PostgreSQL**. Designed for industrial chemical tracking, barcode scanning, shelf capacity verification, and lot expiration monitoring.

> **Note:** This project is an architectural migration of the full-stack [Java/Spring Boot MTS System](https://github.com/FurkanKozmac/mts) to modern .NET, designed for sub-20MB memory footprints and high-throughput async I/O.

## 🏗️ Architecture & Core Principles

- **Clean Layered Architecture:** Clear separation of concerns between Controllers, Application Services, Data Context, and Immutable DTOs.
- **Optimistic Concurrency Control:** Row-versioning on physical units (`Unit.Version`) to prevent race conditions during simultaneous forklift scanning operations.
- **Automated Validation Rules:** System checks prevent moving materials to overflowing shelves (`Address.MaxCapacity`) and block expired batches upon barcode scanning.
- **Zero N+1 Query Overhead:** LINQ projection (`.Select()`) combined with `.AsNoTracking()` minimizes database round-trips and memory overhead.
- **Scalar API Documentation:** Interactive, modern OpenAPI documentation replacing legacy Swagger.

## 📦 Domain Model (7 Core Entities)

1. **Chemical:** Master catalog for chemical definitions, CAS/internal codes, and safety data sheets (MSDS).
2. **Batch:** Inbound lot tracking with supplier details, lot numbers, and expiration dates.
3. **Address:** Warehouse physical storage locations/shelves with capacity constraints.
4. **Unit:** Individual barcoded physical barrels/containers in stock.
5. **RelocationTask:** Traceable workflow state machine managing inventory transfers from shelf to shelf.
6. **User & Role:** Role-Based Access Control (RBAC) separating administrative and warehouse operator duties.

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- [PostgreSQL](https://www.postgresql.org/)

### Installation & Run

```bash
git clone https://github.com/FurkanKozmac/mts-dotnet-backend.git
cd mts-dotnet-backend/backend
dotnet restore
dotnet run
```

Access Scalar API Reference at: `http://localhost:5059/scalar/v1`

## 🛠️ Tech Stack

- **Framework:** ASP.NET Core Web API (.NET 10)
- **Language:** C# 13
- **ORM:** Entity Framework Core (Npgsql)
- **Database:** PostgreSQL
- **Documentation:** Scalar / Microsoft.AspNetCore.OpenApi
