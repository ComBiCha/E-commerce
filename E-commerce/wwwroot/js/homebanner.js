document.addEventListener("DOMContentLoaded", function () {
    const bannerContent = document.querySelector(".banner-content");
    const homeBanner = document.querySelector(".home-banner");

    const bannerTitleWrapper = document.querySelector(".banner-title-wrapper");

    const handleScroll = () => {
        if (window.scrollY > 50) {
            bannerTitleWrapper.classList.add("scrolled");
        } else {
            bannerTitleWrapper.classList.remove("scrolled");
        }
    function handleScroll() {
        const scrollPosition = window.scrollY;
        const bannerHeight = homeBanner.offsetHeight;
        const viewportHeight = window.innerHeight;
        const stopScrollPosition = bannerHeight - viewportHeight;

        if (scrollPosition < stopScrollPosition) {
            const translateY = Math.min(scrollPosition * 0.5, viewportHeight / 2);
            bannerContent.style.transform = `translate(-50%, calc(-65% + ${translateY}px))`;
        } else {
            const fixedTranslateY = stopScrollPosition + 70;
            bannerContent.style.transform = `translate(-50%, calc(-65% + ${fixedTranslateY}px))`;
        }
    }

    function setInitialPosition() {
        bannerContent.style.transform = `translate(-50%, -65%)`;
    }

    setInitialPosition();
    window.addEventListener("scroll", handleScroll);
});
