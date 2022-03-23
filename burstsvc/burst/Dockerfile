ARG BUILD_ENV=
### Build and Test the App
FROM mcr.microsoft.com/dotnet/sdk:5.0-alpine AS build

### copy the source and tests
COPY burst/ /src/burst/

WORKDIR /src/burst

FROM build AS buildunittest
ONBUILD COPY burst.tests/ /src/burst.tests/

# build the app
ONBUILD RUN dotnet test /src/burst.tests/ --collect:'XPlat Code Coverage' --logger 'console;verbosity=normal' --filter 'Category=UnitTest' \
    && dotnet tool install dotnet-reportgenerator-globaltool --tool-path /usr/local/bin \
    &&./../burst.tests/gen_coverage_report.sh

FROM build${BUILD_ENV} as publish
 
RUN dotnet publish -c Release -o /app
###########################################################


### Build the runtime container
FROM mcr.microsoft.com/dotnet/aspnet:5.0-alpine AS release

### if port is changed, also update value in Config
EXPOSE 8080
WORKDIR /app

### create a user
### dotnet needs a home directory
RUN addgroup -S ngsa && \
    adduser -S ngsa -G ngsa && \
    mkdir -p /home/ngsa && \
    chown -R ngsa:ngsa /home/ngsa

### run as ngsa user
USER ngsa

### copy the app
COPY --from=publish /app .

ENTRYPOINT [ "dotnet",  "burstservice.dll" ]
