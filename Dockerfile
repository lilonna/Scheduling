# Use the official Microsoft SQL Server 2022 image
FROM mcr.microsoft.com/mssql/server:2022-latest

# Set environment variables for SQL Server
ENV ACCEPT_EULA=Y
ENV SA_PASSWORD=Railway@1234

# Expose the port SQL Server listens on
EXPOSE 1433

# Run SQL Server
CMD /opt/mssql/bin/sqlservr
