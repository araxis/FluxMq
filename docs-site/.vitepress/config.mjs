export default {
  title: 'FluxMQ',
  description: 'User documentation for FluxMQ',
  base: '/FluxMq/',
  cleanUrls: true,
  appearance: 'dark',
  themeConfig: {
    logo: '/logo.svg',
    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'Workflows', link: '/workflows/record-and-replay' },
      { text: 'Reference', link: '/reference/glossary' }
    ],
    sidebar: [
      {
        text: 'Guide',
        items: [
          { text: 'Getting Started', link: '/guide/getting-started' },
          { text: 'Connections', link: '/guide/connections' },
          { text: 'Recording', link: '/guide/recording' },
          { text: 'Replay', link: '/guide/replay' },
          { text: 'Fork Flow', link: '/guide/fork-flow' }
        ]
      },
      {
        text: 'Workflows',
        items: [
          { text: 'Record And Replay', link: '/workflows/record-and-replay' },
          { text: 'Inspect Payloads', link: '/workflows/inspect-payloads' }
        ]
      },
      {
        text: 'Reference',
        items: [
          { text: 'Glossary', link: '/reference/glossary' }
        ]
      }
    ],
    search: {
      provider: 'local'
    },
    socialLinks: [
      { icon: 'github', link: 'https://github.com/araxis/FluxMq' }
    ],
    footer: {
      message: 'FluxMQ user documentation.',
      copyright: 'Copyright FluxMQ contributors.'
    }
  }
};
