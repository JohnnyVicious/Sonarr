FROM ghcr.io/linuxserver/baseimage-alpine:3.23

RUN apk add --no-cache \
    icu-libs \
    sqlite-libs \
    libintl

COPY --chown=abc:abc Sonarr/ /app/sonarr/bin/
RUN chmod +x /app/sonarr/bin/Sonarr /app/sonarr/bin/ffprobe && \
    rm -rf /app/sonarr/bin/Sonarr.Update

COPY root/ /

ENV COMPlus_EnableDiagnostics=0 \
    TMPDIR=/run/sonarr-temp

VOLUME /config
EXPOSE 8989
