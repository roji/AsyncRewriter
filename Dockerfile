FROM microsoft/aspnet:1.0.0-rc1-update1-coreclr
COPY . /app/
WORKDIR /app
RUN dnu restore --parallel --runtime coreclr src/AsyncRewriter/project.json
RUN dnu build --framework dnxcore50 src/AsyncRewriter/project.json
