#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <arpa/inet.h>
#include <sys/socket.h>
#include <netinet/in.h>

/* 在 50505 收 UDP 廣播,印出來源 IP + payload。argv[1]=要收幾包(預設3) */
int main(int argc, char **argv) {
    int want = argc > 1 ? atoi(argv[1]) : 3;
    int port = 50505;
    int s = socket(AF_INET, SOCK_DGRAM, 0);
    if (s < 0) { perror("socket"); return 2; }
    int one = 1;
    setsockopt(s, SOL_SOCKET, SO_REUSEADDR, &one, sizeof(one));
    setsockopt(s, SOL_SOCKET, SO_BROADCAST, &one, sizeof(one));
    struct sockaddr_in addr;
    memset(&addr, 0, sizeof(addr));
    addr.sin_family = AF_INET;
    addr.sin_addr.s_addr = INADDR_ANY;
    addr.sin_port = htons(port);
    if (bind(s, (struct sockaddr*)&addr, sizeof(addr)) < 0) { perror("bind"); return 3; }
    fprintf(stderr, "LISTEN udp/%d (want %d packets)\n", port, want);
    char buf[2048];
    for (int got = 0; got < want; ) {
        struct sockaddr_in from;
        socklen_t fl = sizeof(from);
        ssize_t n = recvfrom(s, buf, sizeof(buf)-1, 0, (struct sockaddr*)&from, &fl);
        if (n < 0) { perror("recvfrom"); return 4; }
        buf[n] = 0;
        char ip[64];
        inet_ntop(AF_INET, &from.sin_addr, ip, sizeof(ip));
        printf("RECV from %s:%d  %zd bytes: %s\n", ip, ntohs(from.sin_port), n, buf);
        fflush(stdout);
        got++;
    }
    return 0;
}
