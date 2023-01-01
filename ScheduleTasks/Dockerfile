FROM ubuntu
WORKDIR /app
COPY ./ScheduleTasks appsettings.json /app/
RUN chmod +x /app/ScheduleTasks && apt update && apt install libicu-dev libssl-dev -y
ENV ASPNETCORE_URLS http://127.0.0.1:8888
EXPOSE 8888
CMD ["/app/ScheduleTasks"]