FROM alpine:3.23

RUN apk add --no-cache \
    icu-libs \
    sqlite-libs \
    libintl \
    ca-certificates \
    tzdata

RUN addgroup -g 1000 sonarr && \
    adduser -u 1000 -G sonarr -s /bin/sh -D sonarr && \
    mkdir -p /config /run/sonarr-temp && \
    chown -R sonarr:sonarr /config /run/sonarr-temp

COPY --chown=sonarr:sonarr Sonarr/ /app/sonarr/bin/
RUN chmod +x /app/sonarr/bin/Sonarr /app/sonarr/bin/ffprobe && \
    rm -rf /app/sonarr/bin/Sonarr.Update

ENV COMPlus_EnableDiagnostics=0 \
    TMPDIR=/run/sonarr-temp

VOLUME /config
EXPOSE 8989

USER sonarr
WORKDIR /app/sonarr/bin
ENTRYPOINT ["/app/sonarr/bin/Sonarr", "-nobrowser", "-data=/config"]
