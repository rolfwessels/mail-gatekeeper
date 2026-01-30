FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine

# Base Development Packages
RUN apk update \
  && apk upgrade \
  && apk add ca-certificates wget && update-ca-certificates \
  && apk add --no-cache --update \
  git \
  curl \
  wget \
  bash \
  make \
  rsync \
  nano \
  zsh \
  zsh-vcs \
  docker-cli \
  docker-cli-compose \
  openssh \
  && git config --global --add safe.directory /template-dotnet-core-console-app


# zsh
RUN sh -c "$(curl -fsSL https://raw.githubusercontent.com/ohmyzsh/ohmyzsh/master/tools/install.sh)" && \
  git clone https://github.com/zsh-users/zsh-autosuggestions.git /root/.oh-my-zsh/plugins/zsh-autosuggestions && \
  git clone https://github.com/memark/zsh-dotnet-completion.git /root/.oh-my-zsh/plugins/zsh-dotnet-completion && \
  git clone https://github.com/zsh-users/zsh-autosuggestions /root/.oh-my-zsh/custom/plugins/zsh-autosuggestions && \
  echo "done"

# Working Folder
WORKDIR /template-dotnet-core-console-app
# find * | grep '.csproj' | grep -v obj  | sed "s|\(.*\)/\([^/]*\)\.csproj|COPY [\"\1/\2.csproj\", \"\1/\"]|"
COPY ["src/MailGatekeeper.Cmd/MailGatekeeper.Cmd.csproj", "src/MailGatekeeper.Cmd/"]

COPY ["MailGatekeeper.sln", "MailGatekeeper.sln"]
RUN dotnet restore


WORKDIR /template-dotnet-core-console-app
ENV PATH="/root/.dotnet/tools:${PATH}"
ENV TERM xterm-256color
RUN printf 'export PS1="\[\e[0;34;0;33m\][DCKR]\[\e[0m\] \\t \[\e[40;38;5;28m\][\w]\[\e[0m\] \$ "' >> ~/.bashrc
