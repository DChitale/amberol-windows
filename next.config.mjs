const nextConfig = {
  output: "export",
  images: {
    unoptimized: true
  },
  trailingSlash: true,
  webpack: (config) => {
    config.resolve.alias = {
      ...config.resolve.alias,
      "react-native-fs": false
    };
    return config;
  }
};

export default nextConfig;
